using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

// ReSharper disable InconsistentNaming
namespace Sentro.Traffic
{
    /*
        Responsibility : Hold addresses for a connection
    */
    class Connection
    {
        public string Tag = "Connection";

        private State CurrentState;        
        private readonly Diversion diversion;
        private readonly FileLogger fileLogger;        
        private readonly System.Timers.Timer timeoutTimer;

        private int hashcode;
        private uint _sentLength;
        private bool pauseSendingCache;
        private string hashOfLastHttpGet = "";

        public int HashCode
        {
            get { return hashcode; }
            set
            {
                hashcode = value;
                Tag = value.ToString();
            }
        }
        
        
        public Connection(Diversion diversion)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;            
            timeoutTimer = new System.Timers.Timer();
            timeoutTimer.Elapsed += OnTimeout;
            timeoutTimer.Interval = 30*1000;
            timeoutTimer.Enabled = true;
            fileLogger = FileLogger.GetInstance();
            fileLogger.Debug(Tag, "new connection");
        }

        private void OnTimeout(object source,ElapsedEventArgs e)
        {                        
            fileLogger.Debug(Tag,"timeout");
            CurrentState = State.Closed;
            ClearResources();
        }

        public void ClearResources()
        {
            try
            {
                if (cachingFileStream != null)
                {
                    fileLogger.Debug(Tag,"caching file stream not null");
                    if (cachingFileStream != Stream.Null)
                    {
                        cachingFileStream.Flush();
                        cachingFileStream.Close();
                        cachingFileStream.Dispose();
                        cachingFileStream = null;
                    }
                }
                Connection c;
                KvStore.Connections.TryRemove(HashCode, out c);
            }
            catch (Exception e)
            {
                fileLogger.Error(Tag,e.ToString());
            }

            fileLogger.Debug(Tag, "cleared resources");
        }

        private void resetTimer(Packet rawPacket)
        {
            timeoutTimer.Stop();
            timeoutTimer.Start();
            fileLogger.Debug(Tag, $"{rawPacket.SrcIp}:{rawPacket.SrcPort}->{rawPacket.DestIp}:{rawPacket.DestPort}");
        }

        private byte _windowScale;

        public void Add(Packet rawPacket, Address address)
        {
            Task.Run(() =>
            {
                fileLogger.Debug(Tag, "packet in");
                if (rawPacket.SynAck)
                    _windowScale = rawPacket.WindowScale;

                //resetTimer(rawPacket);

                switch (CurrentState)
                {
                    case State.Closed:
                        Closed(rawPacket, address);
                        break;
                    case State.Caching:
                        Caching(rawPacket, address);
                        break;
                    case State.Connected:
                        Connected(rawPacket, address);
                        break;
                    case State.SendingCache:
                        SendingCache(rawPacket, address);
                        break;
                    case State.WaitResponse:
                        WaitResponse(rawPacket, address);
                        break;
                    case State.Transferring:
                        Transferring(rawPacket, address);
                        break;
                }
            });
        }

        private void Closed(Packet rawPacket,Address address)
        {
            CurrentState = State.Closed;
            if (rawPacket.Ack)
            {
                if (IsOut(rawPacket, address))
                {
                    fileLogger.Debug(Tag, "closed: out ack -> connected");
                    Connected(rawPacket, address);
                }
                else
                {
                    fileLogger.Debug(Tag, "closed: out in -> transferring");
                    Transferring(rawPacket, address);
                }
            }
            else
            {
                fileLogger.Debug(Tag, "closed: not ack");
                SendAsync(rawPacket, address);
            }
        }

        private void Connected(Packet rawPacket,Address address)
        {
            CurrentState = State.Connected;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "connected: fin finack rst -> closed");
                Closed(rawPacket, address);
            }
            else if (IsOut(rawPacket, address) && rawPacket.Ack && (rawPacket.DataLength > 0))
            {
                if (!rawPacket.IsHttpGet())
                {
                    fileLogger.Debug(Tag, "connected: not get -> transferring");
                    Transferring(rawPacket, address);
                }
                else
                {
                    hashOfLastHttpGet = rawPacket.Uri.Normalize().MurmurHash();
                    if (CacheManager.IsCached(hashOfLastHttpGet))
                    {
                        fileLogger.Debug(Tag, "connected: get cached -> sending cache");
                        SendingCache(rawPacket, address);
                    }
                    else
                    {
                        fileLogger.Debug(Tag, "connected: get not cached -> wait response");                        
                        WaitResponse(rawPacket,address);
                    }
                }
            }
            else
            {
                fileLogger.Debug(Tag, "connected: in or !ack or 0");
                SendAsync(rawPacket, address);
            }
        }

        private void Transferring(Packet rawPacket,Address address)
        {
            CurrentState = State.Transferring;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "transferring: fin finack rst -> closed");
                Closed(rawPacket, address);
            }
            else
            {
                fileLogger.Debug(Tag, "transferring: ok");
                SendAsync(rawPacket, address);                
            }
        }

        private void WaitResponse(Packet rawPacket,Address address)
        {
            CurrentState = State.WaitResponse;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "waiting: fin finack rst -> closed");
                Closed(rawPacket, address);
            }
            else if (!IsOut(rawPacket, address) && rawPacket.Ack && (rawPacket.DataLength > 0))
            {
                if (rawPacket.IsHttpResponse() && CacheManager.IsCacheable(rawPacket.HttpResponseHeaders))
                {
                    fileLogger.Debug(Tag, "waiting: cachable http response -> caching");
                    Caching(rawPacket, address);
                }
                else
                {
                    fileLogger.Debug(Tag, "waiting: !cachable http response -> transferring");
                    Transferring(rawPacket, address);
                }
            }
            else
            {
                fileLogger.Debug(Tag, "waiting: in !ack 0");
                SendAsync(rawPacket, address);
            }
        }

        private FileStream cachingFileStream;
        private Dictionary<uint, Packet> queuedPackets;
        private int responseContentLength;
        private uint expectedSequence;
        private int cachedContentLength;
        private SemaphoreSlim cachingLock;
        private Queue<Packet> blockQueue;

        private void Caching(Packet rawPacket, Address address)
        {
            CurrentState = State.Caching;
            if (IsOut(rawPacket, address))
            {
                fileLogger.Debug(Tag, "caching: ack out -> return");
                SendAsync(rawPacket, address);
                return;
            }
            if (cachingFileStream == null)
            {
                cachingFileStream = CacheManager.OpenFileWriteStream(hashOfLastHttpGet);
                queuedPackets = new Dictionary<uint, Packet>();
                blockQueue = new Queue<Packet>();
                responseContentLength = rawPacket.HttpResponseHeaders.ContentLength;
                expectedSequence = rawPacket.SeqNumber - (uint) rawPacket.DataLength;
                fileLogger.Debug(Tag, $"caching: firstpacket seq and expected set to {expectedSequence}");
                cachingLock = new SemaphoreSlim(1);
                fileLogger.Debug(Tag, "init cache");
                fileLogger.Debug(Tag, $"response contnetlength : {responseContentLength}");
            }
            blockQueue.Enqueue(rawPacket);
            SendAsync(rawPacket, address);
            cachingLock.Wait();
            Task.Factory.StartNew(() =>
            {
                rawPacket = blockQueue.Dequeue();
                if (!rawPacket.Ack)
                {
                    fileLogger.Debug(Tag, "caching: !ack -> finishing entered lock");
                    if (cachedContentLength != responseContentLength)
                    {
                        while (queuedPackets.ContainsKey(expectedSequence))
                        {
                            rawPacket = queuedPackets[expectedSequence];
                            cachePacket(rawPacket);
                        }
                    }
                    if (cachingFileStream != null)
                    {
                        cachingFileStream.Flush();
                        cachingFileStream.Close();
                        cachingFileStream.Dispose();
                        cachingFileStream = null;
                        queuedPackets.Clear();
                        queuedPackets = null;
                    }
                   
                    if (cachedContentLength < responseContentLength)
                    {
                        CacheManager.Delete(hashOfLastHttpGet);
                        fileLogger.Debug(Tag, "caching: delete cache");
                    }
                    hashOfLastHttpGet = "";
                    responseContentLength = cachedContentLength = 0;
                    expectedSequence = 0;

                    fileLogger.Debug(Tag, "caching: !ack -> closed");
                    CurrentState = State.Closed;                    
                }
                else
                {
                    
                    fileLogger.Debug(Tag, "caching: ack -> entered lock");
                    var packetSeq = rawPacket.SeqNumber - (uint) rawPacket.DataLength;
                    fileLogger.Debug(Tag, $"caching: pacletSeq before task start {packetSeq}");

                    fileLogger.Debug(Tag, $"caching: in task packetSeq {packetSeq} expectedSeq {expectedSequence}");
                    if (packetSeq == expectedSequence || queuedPackets.ContainsKey(expectedSequence))
                    {
                        if (packetSeq != expectedSequence)
                        {
                            fileLogger.Debug(Tag, "caching: packet taken from queue");
                            rawPacket = queuedPackets[expectedSequence];
                            queuedPackets.Remove(expectedSequence);
                        }
                        cachePacket(rawPacket);
                    }
                    else
                    {
                        fileLogger.Debug(Tag, "caching: queued packet");
                        queuedPackets.AddOrReplace(packetSeq, rawPacket);
                    }

                }
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness).ContinueWith(task =>
            {
                cachingLock.Release();
                fileLogger.Debug(Tag, "caching: exit lock !ack");
            });
        }

        private void cachePacket(Packet rawPacket)
        {
            var packetLength = rawPacket.DataLength;
            fileLogger.Debug(Tag,"writing " + packetLength);
            cachingFileStream.Write(rawPacket.RawPacket, rawPacket.DataStart, packetLength);
            cachingFileStream.Flush();
            expectedSequence += (uint)packetLength;
            cachedContentLength += packetLength;
            fileLogger.Debug(Tag, $"write function: expSeq {expectedSequence} cachedLength {cachedContentLength}");
        }
                            
        private void SendingCache(Packet rawPacket,Address address)
        {
            CurrentState = State.SendingCache;
            fileLogger.Debug(Tag, "sendingcache");            
            if (IsOut(rawPacket,null))
            {
                pauseSendingCache = (rawPacket.WindowSize*(1 << _windowScale)) < 3000;
                fileLogger.Debug(Tag,
                    $"sendingCache: paused : windowSize {rawPacket.WindowSize}-> {pauseSendingCache}");
                if (rawPacket.Fin || rawPacket.Rst)
                {                    
                    fileLogger.Debug(Tag, "sendingCache:fin | rst -> closing");
                }
            }
        }        
               
        private void SendCacheResponse(CacheResponse response, Packet requestPacket)
        {
            var random = (ushort) DateTime.Now.Millisecond;

            var seq = requestPacket.AckNumber;
            var ack = requestPacket.SeqNumber + (uint) requestPacket.DataLength;

            foreach (var packet in response.NetworkPackets)
            {
                if (CurrentState.HasFlag(State.Closed))
                {
                    fileLogger.Debug(Tag, "sendcacheasync:closed | closing -> break");
                    break;
                }

                SetFakeHeaders(packet, requestPacket, random++, seq, ack, 0);
                fileLogger.Debug(Tag, "sendcacheasync:fakeheaders -> set");
                SpinWait.SpinUntil(() => pauseSendingCache == false);
                fileLogger.Debug(Tag, "sendcacheasync:spin -> done");
                diversion.CalculateChecksums(packet.RawPacket, packet.RawPacketLength, 0);
                SendAsync(packet,null);
                seq += packet.RawPacketLength - 40;                
            }
            response.Close();            
            fileLogger.Debug(Tag, "sendcacheasync:resp close -> sentcache");
        }

        readonly byte[] secondaryBuffer = new byte[2000];

        private void SetFakeHeaders(Packet response, Packet request, ushort random, uint seq, uint ack, byte fin)
        {
            //ALL WINDOWS versions uses Littile-Endian so reverse is done for Network Byte Order

            //response.IpHeader.Version = 4;
            //response.IpHeader.HeaderLength = 5;
            //0100 0101 -> 69
            response.RawPacket[0] = 69;

            //Diferentiated Services
            //Expidated Forwarding, Not-ECN
            //1011 1000 -> 184
            response.RawPacket[1] = 184;

            //Total Length (2 bytes)
            //reverse byte order
            var length = BitConverter.GetBytes((ushort) response.RawPacketLength);
            response.RawPacket[2] = length[1];
            response.RawPacket[3] = length[0];

            //identification
            //reverse 2 bytes
            var id = BitConverter.GetBytes(random);
            response.RawPacket[4] = id[1];
            response.RawPacket[5] = id[0];

            //Flags and Fragment offset makes 2 bytes and set to 0
            response.RawPacket[6] = 0;
            response.RawPacket[7] = 0;

            //TTL set to 128 as constant default
            response.RawPacket[8] = 128;

            //Protocol set to TCP = 6
            response.RawPacket[9] = 6;

            //IP Header Checksum 2 bytes are calculated out of this function
            //response.RawPacket[10] = 0;
            //response.RawPacket[11] = 0;

            //Source Ip address 4 bytes
            //set to request dest ip
            response.RawPacket[12] = request.RawPacket[16];
            response.RawPacket[13] = request.RawPacket[17];
            response.RawPacket[14] = request.RawPacket[18];
            response.RawPacket[15] = request.RawPacket[19];

            //Destination Ip address 4 bytes     
            //set to request src ip                   
            response.RawPacket[16] = request.RawPacket[12];
            response.RawPacket[17] = request.RawPacket[13];
            response.RawPacket[18] = request.RawPacket[14];
            response.RawPacket[19] = request.RawPacket[15];

            // END OF IP HEADERS

            var tcpStart = (request.RawPacket[0] & 15)*4;

            //Source Port 2 bytes                        
            //set to request dest port
            response.RawPacket[20] = request.RawPacket[tcpStart + 2];
            response.RawPacket[21] = request.RawPacket[tcpStart + 3];

            //Destination Port 2 bytes
            //set to request src port            
            response.RawPacket[22] = request.RawPacket[tcpStart + 0];
            response.RawPacket[23] = request.RawPacket[tcpStart + 1];

            //Sequence number 4 bytes
            //reverse
            var seqBytes = BitConverter.GetBytes(seq);
            response.RawPacket[24] = seqBytes[3];
            response.RawPacket[25] = seqBytes[2];
            response.RawPacket[26] = seqBytes[1];
            response.RawPacket[27] = seqBytes[0];

            //Acknoldgment number 4 bytes
            //reverse
            var ackBytes = BitConverter.GetBytes(ack);
            response.RawPacket[28] = ackBytes[3];
            response.RawPacket[29] = ackBytes[2];
            response.RawPacket[30] = ackBytes[1];
            response.RawPacket[31] = ackBytes[0];

            //Data offset , reserved , NS flag
            // 20 bytes are 5(0101) words , reserved 0 0 0 , ns 0
            //0101 0000 -> 80
            response.RawPacket[32] = 80;

            //flags are ack (16) and psh (8) = 24
            response.RawPacket[33] = (byte) (24 + fin);

            //windows size 2 bytes
            //set 16383 as defualt
            var windowSize = BitConverter.GetBytes((ushort) 16383);
            response.RawPacket[34] = windowSize[1];
            response.RawPacket[35] = windowSize[0];

            //checksum calculated out of this function 2bytes
            //response.RawPacket[36] = 0;
            //response.RawPacket[37] = 0;

            //urgent pointer 2 bytes
            response.RawPacket[38] = 0;
            response.RawPacket[39] = 0;

            //END OF TCP 

            //END OF 40 Bytes headers 
        }

        private void SendAsync(Packet rawPacket,Address address)
        {
            //fileLogger.Debug(Tag,"send async");
            //diversion.SendAsync(rawPacket.RawPacket, rawPacket.RawPacketLength, address, ref _sentLength);
        }

        private bool IsOut(Packet rawPacket, Address address)
        {
            var destPort = rawPacket.DestPort;
            return (address.Direction == DivertDirection.Outbound && (destPort == 80 || destPort == 443));
        }

        [Flags]
        private enum State
        {
            Closed          = 1 << 0,
            Connected       = 1 << 1,
            Transferring    = 1 << 2,
            Caching         = 1 << 3,
            SendingCache    = 1 << 4,
            WaitResponse    = 1 << 5
        }
    }
}
