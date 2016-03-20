using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

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

        private Address address;
        public Connection(Diversion diversion,Address address)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;            
            timeoutTimer = new System.Timers.Timer();
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
                            cachingFileStream.Flush();
                            cachingFileStream.Close();
                            cachingFileStream.Dispose();
                            cachingFileStream = null;
                        }
                    }
                    catch (Exception e)
                    {

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
            
        }

        private byte _windowScale = 5;

        public void Add(Packet rawPacket,Address addressLoop)
        {
            address = addressLoop;
            if (rawPacket.SynAck)
            {
                _windowScale = rawPacket.WindowScale;
                _windowScale = _windowScale == 0 ? (byte) 5 : _windowScale;
            }

            resetTimer(rawPacket);
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
            fileLogger.Debug(Tag, "closed");
            CurrentState = State.Closed;
            if (rawPacket.Ack)
                if (IsOut(rawPacket))
                    Connected(rawPacket);
                else
                    Transferring(rawPacket);
            else
                Send(rawPacket);
        }

        private void Connected(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "connected");
            CurrentState = State.Connected;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {                
                CurrentState = State.Closed;
                Send(rawPacket);
            }
            else if (IsOut(rawPacket) && rawPacket.Ack && (rawPacket.DataLength > 1))           
                if (!rawPacket.IsHttpGet())
                    Transferring(rawPacket);
                else
                {
                    hashOfLastHttpGet = rawPacket.Uri.Normalize().MurmurHash();
                    if (CacheManager.IsCached(hashOfLastHttpGet))
                        SendingCache(rawPacket);
                    else
                        WaitResponse(rawPacket);
                }            
            else
                Send(rawPacket);            
        }

        private void Transferring(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "transferring");
            CurrentState = State.Transferring;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
                CurrentState = State.Closed;

            Send(rawPacket);
        }

        private void WaitResponse(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "wait response");
            CurrentState = State.WaitResponse;
            if (rawPacket.Fin || rawPacket.FinAck || rawPacket.Rst)
            {
                CurrentState = State.Closed;
                Send(rawPacket);
            }
            else if (IsIn(rawPacket) && rawPacket.Ack && (rawPacket.DataLength > 1))            
                if (rawPacket.IsHttpResponse() && CacheManager.IsCacheable(rawPacket.HttpResponseHeaders))                                    
                    Caching(rawPacket);                
                else                                    
                    Transferring(rawPacket);                            
            else                            
                Send(rawPacket);            
        }

        private FileStream cachingFileStream;
        private Dictionary<uint, Packet> queuedPackets;
        private int responseContentLength;
        private uint expectedSequence;
        private int cachedContentLength;        
        private void Caching(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "caching");
            CurrentState = State.Caching;

            if (cachingFileStream == null)
            {
                cachingFileStream = CacheManager.OpenFileWriteStream(hashOfLastHttpGet);
                queuedPackets = new Dictionary<uint, Packet>();                         
                responseContentLength = rawPacket.HttpResponseHeaders.ContentLength;
                expectedSequence = rawPacket.SeqNumber;                
            }

            Send(rawPacket);
            if (IsOut(rawPacket))            
                return;            
    
            if (rawPacket.Ack)
            {
                var packetSeq = rawPacket.SeqNumber;
                if (packetSeq == expectedSequence || queuedPackets.ContainsKey(expectedSequence))
                {
                    if (packetSeq != expectedSequence)
                    {
                        rawPacket = queuedPackets[expectedSequence];
                        queuedPackets.Remove(expectedSequence);
                    }                                                                
                    cachePacket(rawPacket);
                }
                else
                    queuedPackets.AddOrReplace(packetSeq, rawPacket);
            }
            else
            {
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
                    cachingFileStream.Dispose();
                    cachingFileStream = null;
                    queuedPackets.Clear();
                    queuedPackets = null;
                }

                if (cachedContentLength < responseContentLength)
                    CacheManager.Delete(hashOfLastHttpGet);

                hashOfLastHttpGet = "";
                responseContentLength = cachedContentLength = 0;
                expectedSequence = 0;
                
                CurrentState = State.Closed;
            }          
        }

        private void cachePacket(Packet rawPacket)
        {            
            fileLogger.Debug(Tag, "writepacket");
            var packetLength = rawPacket.DataLength;
            expectedSequence += (uint) packetLength;
            cachedContentLength += packetLength;
            cachingFileStream.Write(rawPacket.RawPacket, rawPacket.DataStart, packetLength);
            cachingFileStream.Flush();            
        }

        private CacheResponse cacheResponse;
        private ushort random;
        private uint seq, ack,limitWindowSize = 2000;
        private Packet savedRequestPacket;

        private void SendingCache(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "sendingcache");
            CurrentState = State.SendingCache;
            if (cacheResponse == null)
            {
                cacheResponse = CacheManager.Get(hashOfLastHttpGet);
                random = (ushort) DateTime.Now.Millisecond;
                seq = rawPacket.AckNumber;
                ack = rawPacket.SeqNumber + (uint) rawPacket.DataLength;
                savedRequestPacket = rawPacket;
            }

            if (IsIn(rawPacket))
                return;

            fileLogger.Debug(Tag, "winsize : " + rawPacket.WindowSize + " scale " + _windowScale);
            if ((uint) (rawPacket.WindowSize*(1 << _windowScale)) < limitWindowSize)
                return;

            //while (true)
            //{
                var nextPacket = cacheResponse.NextPacket();
                if (nextPacket != null)
                {
                    SetFakeHeaders(nextPacket, savedRequestPacket, random++, seq, ack, 0);
                    diversion.CalculateChecksums(nextPacket.RawPacket, nextPacket.RawPacketLength, 0);
                    SendAsync(nextPacket);
                    seq += nextPacket.RawPacketLength - 40;
                    return;
                }
                //else
                //{
                //    break;
                //}
            //}


            //var finPacket = new Packet(new byte[40], 40);
            //SetFakeHeaders(finPacket, savedRequestPacket, random++, seq, ack, 1);
            //diversion.CalculateChecksums(finPacket.RawPacket, finPacket.RawPacketLength, 0);
            //SendAsync(finPacket);

            fileLogger.Debug(Tag, "end of cache");
            cacheResponse.Close();
            cacheResponse = null;
            CurrentState = State.Transferring;
        }

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

        private void Send(Packet rawPacket)
        {            
            diversion.Send(rawPacket.RawPacket, rawPacket.RawPacketLength,address, ref _sentLength);
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
