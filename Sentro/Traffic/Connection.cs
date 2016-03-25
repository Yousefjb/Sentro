using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;
using Timer = System.Timers.Timer;

// ReSharper disable InconsistentNaming
namespace Sentro.Traffic
{
    /*
        Responsibility : Hold es for a connection
    */
    class Connection
    {
        public string Tag = "Connection";

        private State CurrentState;        
        private readonly Diversion diversion;
        private readonly FileLogger fileLogger;        
        private readonly Timer timeoutTimer;

        private int hashcode;
        private uint _sentLength;        
        private string hashOfLastHttpGet = "";

        public int HashCode
        {            
            set
            {
                hashcode = value;
                Tag = value.ToString();
            }
        }

        private Address address;
        public Connection(Diversion diversion,Address address)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;            
            timeoutTimer = new Timer();
            timeoutTimer.Elapsed += OnTimeout;
            timeoutTimer.Interval = 30*1000;
            timeoutTimer.Enabled = true;
            fileLogger = FileLogger.GetInstance();
            fileLogger.Debug(Tag, "new connection");
            this.address = address;
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
                    try
                    {
                        if (cachingFileStream != Stream.Null)
                        {                            
                            cachingFileStream.Dispose();
                            cachingFileStream = null;
                        }
                    }
                    catch (Exception e)
                    {
                                                                      
                    }
                }
                Connection c;
                KvStore.Connections.TryRemove(hashcode, out c);
            }
            catch (Exception e)
            {
                fileLogger.Error(Tag,e.ToString());
            }

            fileLogger.Debug(Tag, "cleared resources");
        }
  
        private byte _windowScale = 5;

        public void Add(Packet rawPacket, Address addressLoop)
        {
            address = addressLoop;
            if (rawPacket.Syn)
            {
                _windowScale = rawPacket.WindowScale;
                _windowScale = _windowScale == 0 ? (byte) 5 : _windowScale;
            }

            timeoutTimer.Stop();
            timeoutTimer.Start();
            fileLogger.Debug(Tag, $"{rawPacket.SrcIp}:{rawPacket.SrcPort}->{rawPacket.DestIp}:{rawPacket.DestPort}");
            switch (CurrentState)
            {
                case State.Closed:
                    Closed(rawPacket);
                    break;
                case State.Caching:
                    Caching(rawPacket);
                    break;
                case State.Connected:
                    Connected(rawPacket);
                    break;
                case State.SendingCache:
                    SendingCache(rawPacket);
                    break;
                case State.WaitResponse:
                    WaitResponse(rawPacket);
                    break;
                case State.Transferring:
                    Transferring(rawPacket);
                    break;
                default:
                    SendAsync(rawPacket);
                    break;
            }
        }

        private void Closed(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- closed -----");
            CurrentState = State.Closed;
            if (rawPacket.Ack)
            {
                if (IsOut(rawPacket))
                {
                    fileLogger.Debug(Tag, "connect ack out");
                    Connected(rawPacket);
                }
                else
                {
                    fileLogger.Debug(Tag, "transfare ack in");
                    Transferring(rawPacket);
                }
            }
            else
            {
                fileLogger.Debug(Tag, "send not ack");
                SendAsync(rawPacket);
            }
        }

        private void Connected(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- connected -----");
            CurrentState = State.Connected;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "fin | rst change to closes and send");
                CurrentState = State.Closed;
                SendAsync(rawPacket);
            }
            else if (IsOut(rawPacket) && rawPacket.Ack && (rawPacket.DataLength > 1))
            {
                if (!rawPacket.IsHttpGet())
                {
                    fileLogger.Debug(Tag, "ack out not http get, go to transfare");
                    Transferring(rawPacket);
                }
                else
                {
                    hashOfLastHttpGet = rawPacket.Uri.NormalizeUrl().MurmurHash();
                    Console.WriteLine(hashOfLastHttpGet + " " + rawPacket.Uri);
                    fileLogger.Debug(Tag, $"out ack http get : {rawPacket.Uri}");
                    if (CacheManager.IsCached(hashOfLastHttpGet))
                    {
                        fileLogger.Debug(Tag, "request is cached");
                        var ackpacket = GetAckPacket(rawPacket);
                        SetChecksum(ackpacket);
                        ackpacket = AddEthernetPadding(ackpacket);
                        SendAsync(ackpacket);
                        fileLogger.Debug(Tag, "send request recived ack with padding");
                        SendingCache(rawPacket);
                    }
                    else
                    {
                        fileLogger.Debug(Tag, "request is not cached");
                        WaitResponse(rawPacket);
                    }
                }
            }
            else
            {
                fileLogger.Debug(Tag, "send packet [ in | not ack | 0 length");
                SendAsync(rawPacket);
            }
        }

        private void Transferring(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- transferring -----");
            CurrentState = State.Transferring;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "go closed state : fin | rst");
                CurrentState = State.Closed;
            }

            fileLogger.Debug(Tag, "send");
            SendAsync(rawPacket);
        }             

        private void WaitResponse(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "wait response");
            CurrentState = State.WaitResponse;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                fileLogger.Debug(Tag, "send fin | rst");
                CurrentState = State.Closed;
                SendAsync(rawPacket);
            }
            else if (IsIn(rawPacket) && rawPacket.Ack && (rawPacket.DataLength > 1))
            {
                if (rawPacket.IsHttpResponse() && CacheManager.IsCacheable(rawPacket.HttpResponseHeaders))
                {
                    fileLogger.Debug(Tag, "cachable http response");
                    Caching(rawPacket);
                }
                else
                {
                    fileLogger.Debug(Tag, "not http response or not cachable");
                    Transferring(rawPacket);
                }
            }
            else
            {
                fileLogger.Debug(Tag, "send out | not ack | 0 length");
                SendAsync(rawPacket);
            }
        }

        private FileStream cachingFileStream;
        private Dictionary<uint, Packet> queuedPackets;
        private int responseContentLength;
        private uint expectedSequence;
        private int cachedContentLength;        
        private bool firstPacketToCache;   
        private void Caching(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- caching -----");
            CurrentState = State.Caching;

            if (cachingFileStream == null)
            {
                fileLogger.Debug(Tag, "init http response for cache");
                cachingFileStream = CacheManager.OpenFileWriteStream(hashOfLastHttpGet);
                queuedPackets = new Dictionary<uint, Packet>();                         
                responseContentLength = rawPacket.HttpResponseHeaders.ContentLength;
                expectedSequence = rawPacket.SeqNumber;
                firstPacketToCache = true;
            }

            SendAsync(rawPacket);
            if (IsOut(rawPacket))            
                return;

            
            if (rawPacket.DataLength > 0)
            {
                var packetSeq = rawPacket.SeqNumber;
                if (packetSeq == expectedSequence || queuedPackets.ContainsKey(expectedSequence))
                {
                    if (packetSeq != expectedSequence)
                    {
                        rawPacket = queuedPackets[expectedSequence];
                        queuedPackets.Remove(expectedSequence);                        
                    }
                    cachePacket(rawPacket, firstPacketToCache);
                    firstPacketToCache = false;
                }
                else
                {
                    fileLogger.Debug(Tag, "queued packet");
                    queuedPackets.AddOrReplace(packetSeq, rawPacket);                    
                    Console.WriteLine("pq {0}",queuedPackets.Count);
                }
            }
            else
            {
                fileLogger.Debug(Tag, "cache stopped due to not ack");
                if (cachedContentLength != responseContentLength)
                {
                    fileLogger.Debug(Tag, "incomplete cache,looking in queue");
                    while (queuedPackets.ContainsKey(expectedSequence))
                    {
                        rawPacket = queuedPackets[expectedSequence];
                        cachePacket(rawPacket,firstPacketToCache);
                        firstPacketToCache = false;
                    }
                    fileLogger.Debug(Tag, "queue doesn't have expepected seq");
                }
                if (cachingFileStream != null)
                {
                    fileLogger.Debug(Tag, "dispose cache file and clear queue");
                    cachingFileStream.Flush();
                    cachingFileStream.Dispose();
                    cachingFileStream = null;
                    queuedPackets.Clear();
                    queuedPackets = null;
                }

                if (cachedContentLength < responseContentLength)
                {
                    fileLogger.Debug(Tag, $"incomple {hashOfLastHttpGet}, delete the cache");
                    Console.WriteLine("deleting {0}",hashOfLastHttpGet);
                    CacheManager.Delete(hashOfLastHttpGet);
                }

                hashOfLastHttpGet = "";
                responseContentLength = cachedContentLength = 0;
                expectedSequence = 0;
                
                CurrentState = State.Closed;
            }          
        }

        private void cachePacket(Packet rawPacket,bool writeLengthFirst)
        {
            var packetLength = rawPacket.DataLength;
            fileLogger.Debug(Tag, $"disk write {packetLength} bytes");
            expectedSequence += (uint) packetLength;
            cachedContentLength += packetLength;
            if (writeLengthFirst)
            {
                byte[] bytes = {(byte) (rawPacket.DataLength >> 8), (byte) rawPacket.DataLength};
                cachingFileStream.Write(bytes, 0, 2);
            }
            cachingFileStream.Write(rawPacket.RawPacket, rawPacket.DataStart, packetLength);
            cachingFileStream.Flush();            
        }

        private CacheResponse cacheResponse;
        private ushort random;
        private uint seq, ack,limitWindowSize;
        private Packet savedRequestPacket;
        private byte firstCachedPacketToSend;        
        private ushort ipChecksum = 0;
        private SemaphoreSlim seqSemaphore;
        private uint lastAck, lastAckRepetetion;
        private Dictionary<uint, Packet> cachedSentPackets;
        private void SendingCache(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- sending cache -----");
            CurrentState = State.SendingCache;
            if (cacheResponse == null)
            {
                fileLogger.Debug(Tag, "get cache response from disk");
                cacheResponse = CacheManager.Get(hashOfLastHttpGet);
                random = (ushort) DateTime.Now.Millisecond;
                seq = rawPacket.AckNumber;
                ack = rawPacket.SeqNumber + (uint) rawPacket.DataLength;
                savedRequestPacket = rawPacket;
                firstCachedPacketToSend = 8;
                seqSemaphore = new SemaphoreSlim(1, 1);
                cachedSentPackets = new Dictionary<uint, Packet>();
            }

            if (IsIn(rawPacket))
            {
                fileLogger.Debug(Tag, "dropped in packet");
                return;
            }
            
            if (lastAck == rawPacket.AckNumber)
            {
                lastAckRepetetion++;
                if (lastAckRepetetion >= 3)
                    if (cachedSentPackets.ContainsKey(lastAck))
                        SendAsync(cachedSentPackets[lastAck]);
            }
            else
            {
                lastAck = rawPacket.AckNumber;
                lastAckRepetetion = 0;
            }

            if (rawPacket.AckNumber != seq)
            {               
                return;
            }

            var isTarget = KvStore.TargetIps.Contains(rawPacket.SrcIp.AsString());
            uint count = 0;
            uint calculatedWinSize = (uint) (rawPacket.WindowSize*(1 << _windowScale));

            if (isTarget)
                limitWindowSize = 3000;
            else limitWindowSize = 2000;

            count = calculatedWinSize/limitWindowSize;            
            fileLogger.Debug(Tag, $"will send {count} packets");
            if (calculatedWinSize < limitWindowSize)
            {
                fileLogger.Debug(Tag, "window size is low,dont send");
                return;
            }
            try
            {
                Parallel.For(0, count, (i, loopstate) =>
                {
                    if (cacheResponse != null)
                    {
                        seqSemaphore?.Wait();
                        var nextPacket = cacheResponse?.NextPacket();
                        if (nextPacket != null)
                        {
                            SetFakeHeaders(nextPacket, savedRequestPacket, random++, seq, ack, firstCachedPacketToSend);
                            seq += nextPacket.RawPacketLength - 40;
                            seqSemaphore?.Release();
                            SetChecksum(nextPacket, ipChecksum);
                            SendAsync(nextPacket);  
                            cachedSentPackets.Add(nextPacket.SeqNumber,nextPacket);                          
                            firstCachedPacketToSend = 0;
                        }
                        else
                        {
                            fileLogger.Debug(Tag, "close cache file");
                            cacheResponse?.Close();
                            cacheResponse = null;
                            CurrentState = State.Closed;
                            seqSemaphore?.Release();
                            loopstate.Stop();
                        }
                    }
                });
            }
            catch (Exception)
            {

            }
        }

        private Packet GetAckPacket(Packet request)
        {
            var ackPacket = new Packet(new byte[40], 40);
            ushort fakeId = request.Id;
            fakeId++;
            var fakeSeq = request.AckNumber;
            var fakeAck = request.SeqNumber + (uint) request.DataLength;
            SetFakeHeaders(ackPacket, request, fakeId, fakeSeq, fakeAck, 0);
            return ackPacket;
        }

        private Packet AddEthernetPadding(Packet rawPacket)
        {
            int zerosCount = 60 - (int)rawPacket.RawPacketLength - 14;
            var bytes = new byte[rawPacket.RawPacketLength + zerosCount];
            for (int i = zerosCount; i < rawPacket.RawPacketLength; i++)
                bytes[i] = rawPacket.RawPacket[i];
            return new Packet(bytes, (uint) bytes.Length);
        }

        private void SetFakeHeaders(Packet response, Packet request, ushort fakeRandom, uint fakeSeq, uint fakeAck, byte Flag)
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
            var id = BitConverter.GetBytes(fakeRandom);
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

            //Source Ip  4 bytes
            //set to request dest ip
            response.RawPacket[12] = request.RawPacket[16];
            response.RawPacket[13] = request.RawPacket[17];
            response.RawPacket[14] = request.RawPacket[18];
            response.RawPacket[15] = request.RawPacket[19];

            //Destination Ip  4 bytes     
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
            var seqBytes = BitConverter.GetBytes(fakeSeq);
            response.RawPacket[24] = seqBytes[3];
            response.RawPacket[25] = seqBytes[2];
            response.RawPacket[26] = seqBytes[1];
            response.RawPacket[27] = seqBytes[0];

            //Acknoldgment number 4 bytes
            //reverse
            var ackBytes = BitConverter.GetBytes(fakeAck);
            response.RawPacket[28] = ackBytes[3];
            response.RawPacket[29] = ackBytes[2];
            response.RawPacket[30] = ackBytes[1];
            response.RawPacket[31] = ackBytes[0];

            //Data offset , reserved , NS flag
            // 20 bytes are 5(0101) words , reserved 0 0 0 , ns 0
            //0101 0000 -> 80
            response.RawPacket[32] = 80;

            //flags are ack (16) and psh (8) = 24
            if (response.DataLength < 1420 && Flag == 0)
                Flag = 8;
            response.RawPacket[33] = (byte) (16 + Flag);

            //windows size 2 bytes
            //set 32383 as defualt
            var windowSize = BitConverter.GetBytes((ushort) 32383);
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

        private void SetChecksum(Packet rawPacket)
        {
            diversion.CalculateChecksums(rawPacket.RawPacket, rawPacket.RawPacketLength, 0);
        }

        private void SetChecksum(Packet rawPacket, ushort ipchecksum)
        {
            if (ipchecksum == 0)
                ipchecksum = CalculateIpChecksum(rawPacket);

            rawPacket.RawPacket[10] = (byte)(ipchecksum >> 8);
            rawPacket.RawPacket[11] = (byte) ipchecksum;

            uint checksum = 0;

            var rawPacketLegnth = rawPacket.RawPacketLength;
            
            int i=20;
            for (; i < rawPacketLegnth; i += 2)
                checksum += ((uint)((rawPacket.RawPacket[i] << 8) | rawPacket.RawPacket[i + 1]));

            if(i != rawPacketLegnth)
                checksum += ((uint)(rawPacket.RawPacket[i] << 8));

            for (i = 12; i < 20; i += 2)
                checksum += ((uint)((rawPacket.RawPacket[i] << 8) | rawPacket.RawPacket[i+1]));
            checksum += 6;
            checksum += (rawPacket.RawPacketLength - 20);

            while (checksum >> 16 != 0)
                checksum = (checksum & 0xffff) + (checksum >> 16);


            checksum = ~checksum;
            var result = (ushort)checksum;
            rawPacket.RawPacket[36] = (byte)(result >> 8);
            rawPacket.RawPacket[37] = (byte) result;
        }

        private ushort CalculateIpChecksum(Packet rawPacket)
        {
            uint checksum = 0;
            for (int i = 0; i < 20; i += 2)
                checksum += ((uint)((rawPacket.RawPacket[i] << 8) | rawPacket.RawPacket[i + 1]));

            var carry = checksum >> 16;
            checksum &= 0xffff;
            while (carry > 0)
            {
                checksum += carry;
                checksum &= 0xffff;
                carry = checksum >> 16;
            }

            checksum = ~checksum;
            return (ushort)checksum;
        }

        private void SendAsync(Packet rawPacket)
        {                        
            diversion.SendAsync(rawPacket.RawPacket, rawPacket.RawPacketLength, address, ref _sentLength);
        }       

        private bool IsOut(Packet rawPacket)
        {
            var destPort = rawPacket.DestPort;
            return (destPort == 80 || destPort == 443);
        }

        private bool IsIn(Packet rawPacket)
        {
            return !IsOut(rawPacket);
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
