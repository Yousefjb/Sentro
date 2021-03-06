﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private SemaphoreSlim IOSemaphore;
        private Address address;
        public Connection(Diversion diversion, Address address)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;
            timeoutTimer = new Timer();
            timeoutTimer.Elapsed += OnTimeout;
            timeoutTimer.Interval = 30 * 1000;
            timeoutTimer.Enabled = true;
            fileLogger = FileLogger.GetInstance();
            fileLogger.Debug(Tag, "new connection");
            this.address = address;
            IOSemaphore = new SemaphoreSlim(1, 1);
        }

        private void OnTimeout(object source, ElapsedEventArgs e)
        {
            fileLogger.Debug(Tag, "timeout");
            CurrentState = State.Closed;
            timeoutTimer.Stop();
            ClearResources();
        }

        public void ClearResources()
        {
            try
            {
                if (cachingFileStream != null)
                {
                    fileLogger.Debug(Tag, "caching file stream not null");
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
                fileLogger.Error(Tag, e.ToString());
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
                _windowScale = _windowScale == 0 ? (byte)5 : _windowScale;
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
                case State.MISSEDPACKETS:
                    sendMissedPacket(rawPacket);
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
            if (IsOut(rawPacket) && rawPacket.Ack && (rawPacket.DataLength > 1))
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
                Console.WriteLine(responseContentLength);              
                firstPacketToCache = true;
                cachedContentLength = -ExcludeHeadersLength(rawPacket);
            }

            SendAsync(rawPacket);
            if (IsOut(rawPacket))
                return;

            if (rawPacket.DataLength > 0)
            {
                queuedPackets[rawPacket.SeqNumber] = rawPacket;
            }
            else
            {
                FreeCurrentCacheQueue();
                ClearCurrentCachingState();
                CurrentState = State.Connected;
            }
        }

        private int ExcludeHeadersLength(Packet rawPacket)
        {
            var shit = Encoding.ASCII.GetString(rawPacket.RawPacket);
            var i = shit.IndexOf("\r\n\r\n", StringComparison.InvariantCulture);
            i += 4;
            return Encoding.ASCII.GetBytes(shit.ToCharArray(), 0, i).Length - rawPacket.DataStart;
        }

        private void ClearCurrentCachingState()
        {
            if (cachingFileStream != null)
            {                
                cachingFileStream.Flush();
                cachingFileStream.Dispose();
                cachingFileStream = null;
                queuedPackets.Clear();
                queuedPackets = null;
            }

            if (cachedContentLength != responseContentLength)
            {                                
                Console.WriteLine("deleting {0}", hashOfLastHttpGet);
                CacheManager.Delete(hashOfLastHttpGet);
            }
           
            hashOfLastHttpGet = "";
        }

        private void FreeCurrentCacheQueue()
        {
            var keys = queuedPackets.Select(x => x.Key).OrderBy(u => u);
            HashSet<uint> packetsSeqSet = new HashSet<uint>();
            foreach (var key in keys)
            {                
                var rawPacket = queuedPackets[key];                
                if(packetsSeqSet.Contains(rawPacket.SeqNumber))
                    continue;

                packetsSeqSet.Add(rawPacket.SeqNumber);
                cachePacket(rawPacket, firstPacketToCache);
                queuedPackets.Remove(key);
                firstPacketToCache = false;
            }            
        } 

        private void cachePacket(Packet rawPacket, bool writeLengthFirst)
        {
            IOSemaphore.Wait();
            var packetLength = rawPacket.DataLength;
            fileLogger.Debug(Tag, $"disk write {packetLength} bytes");            
            cachedContentLength += packetLength;
            if (writeLengthFirst)
            {
                byte[] bytes = { (byte)(rawPacket.DataLength), (byte)(rawPacket.DataLength << 8) };
                cachingFileStream.Write(bytes, 0, 2);
            }
            cachingFileStream.Write(rawPacket.RawPacket, rawPacket.DataStart, packetLength);
            cachingFileStream.Flush();
            IOSemaphore.Release();
        }
               
        private CacheResponse cacheResponse;
        private ushort random;
        private uint seq, ack, limitWindowSize;
        private Packet savedRequestPacket;
        private byte firstCachedPacketToSend;
        private SemaphoreSlim seqSemaphore;
        private uint lastAck, lastAckRepetetion;
        private ConcurrentDictionary<uint, Packet> cachedSentPackets;
        private bool allowSendCache = true;
        private int sentCacheSize = 0;
        private void SendingCache(Packet rawPacket)
        {
            fileLogger.Debug(Tag, "----- sending cache -----");
            CurrentState = State.SendingCache;
            if (cacheResponse == null && allowSendCache)
            {
                fileLogger.Debug(Tag, "get cache response from disk");
                cacheResponse = CacheManager.Get(hashOfLastHttpGet);
                random = (ushort) DateTime.Now.Millisecond;
                seq = rawPacket.AckNumber;
                ack = rawPacket.SeqNumber + (uint) rawPacket.DataLength;
                savedRequestPacket = rawPacket;
                firstCachedPacketToSend = 8;
                seqSemaphore = new SemaphoreSlim(1, 1);
                cachedSentPackets = new ConcurrentDictionary<uint, Packet>();
            }

            if (IsIn(rawPacket))
            {
                fileLogger.Debug(Tag, "dropped in packet");
                SendAsync(GetRstPacket(rawPacket));
                return;
            }

            sendMissedPacket(rawPacket);

            if (rawPacket.FinAck)
            {
                SendAsync(GetAckPacket(rawPacket));
                return;
                //CurrentState = State.Closed;
            }

            if (rawPacket.AckNumber != seq || (rawPacket.AckNumber == seq && cacheResponse == null))
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
                        
            //count = count > 15 ? 15 : count;

            fileLogger.Debug(Tag, $"will send {count} packets");
            if (calculatedWinSize < limitWindowSize)
            {
                fileLogger.Debug(Tag, "window size is low,dont send");
                return;
            }
            try
            {
                fileLogger.Debug(Tag, $"count: {count}");                
                Parallel.For(0, count, (i, loopstate) =>
                {                    
                    fileLogger.Debug(Tag, "in parallel");
                    if (cacheResponse != null)
                    {
                        seqSemaphore?.Wait();                        
                        var nextPacket = cacheResponse?.NextPacket();                        
                        if (nextPacket != null)
                        {
                            fileLogger.Debug(Tag, "next packet not null");
                            SetFakeHeaders(nextPacket, savedRequestPacket, random++, seq, ack,
                                firstCachedPacketToSend);
                            seq += nextPacket.RawPacketLength - 40;
                            SetChecksum(nextPacket, 0);
                            SendAsync(nextPacket);
                            seqSemaphore?.Release();
                            cachedSentPackets.TryAdd(nextPacket.SeqNumber, nextPacket);
                            firstCachedPacketToSend = 0;
                            sentCacheSize++;
                        }
                        else
                        {
                            fileLogger.Debug(Tag, "close cache file");
                            cacheResponse?.Close();
                            if (sentCacheSize > 0)
                            {
                                CurrentState = State.MISSEDPACKETS;
                                cacheResponse = null;
                            }                                                        
                            
                            seqSemaphore?.Release();
                            loopstate.Stop();
                        }
                    }
                    else
                    {
                        loopstate.Stop();
                        fileLogger.Debug(Tag, "cache response null");
                    }                    
                });                
            }
            catch (Exception e)
            {
                fileLogger.Debug(Tag, e.ToString());
            }
        }

        private void sendMissedPacket(Packet rawPacket)
        {
            if (!rawPacket.Ack)
            {
                SendAsync(GetRstPacket(rawPacket));
                CurrentState = State.Closed;
            }

            if (lastAck == rawPacket.AckNumber)
            {
                lastAckRepetetion++;
                Console.WriteLine("missed count : "+lastAckRepetetion);
                if (lastAckRepetetion > 1)
                    if (cachedSentPackets.ContainsKey(lastAck))
                    {
                        SendAsync(cachedSentPackets[lastAck]);
                    }
                    else
                    {
                        SendingCache(savedRequestPacket);
                    }
            }
            else
            {
                lastAck = rawPacket.AckNumber;
                lastAckRepetetion = 0;
            }
        }

        private Packet GetAckPacket(Packet request,byte flags = 0)
        {
            var ackPacket = new Packet(new byte[40], 40);
            ushort fakeId = request.Id;
            fakeId++;
            var fakeSeq = request.AckNumber;
            var fakeAck = request.SeqNumber + (uint)request.DataLength;
            SetFakeHeaders(ackPacket, request, fakeId, fakeSeq, fakeAck, flags);
            return ackPacket;
        }

        private Packet GetRstPacket(Packet anyPacket)
        {
            var rstPacket = new Packet(new byte[40], 40);
            ushort fakeId = anyPacket.Id;
            fakeId++;
            var fakeSeq = anyPacket.AckNumber;
            var fakeAck = anyPacket.SeqNumber + (uint)anyPacket.DataLength;
            SetFakeHeaders(rstPacket, anyPacket, fakeId, fakeSeq, fakeAck, 4);
            SetChecksum(rstPacket,0);
            return rstPacket;
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
            var length = BitConverter.GetBytes((ushort)response.RawPacketLength);
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

            var tcpStart = (request.RawPacket[0] & 15) * 4;

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
            response.RawPacket[33] = (byte)(16 + Flag);            

            //windows size 2 bytes
            //set 32383 as defualt
            var windowSize = BitConverter.GetBytes((ushort)32383);
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
            rawPacket.RawPacket[11] = (byte)ipchecksum;

            uint checksum = 0;

            var rawPacketLegnth = rawPacket.RawPacketLength;

            int i = 20;
            for (; i < rawPacketLegnth; i += 2)
                checksum += ((uint)((rawPacket.RawPacket[i] << 8) | rawPacket.RawPacket[i + 1]));

            if (i != rawPacketLegnth)
                checksum += ((uint)(rawPacket.RawPacket[i] << 8));

            for (i = 12; i < 20; i += 2)
                checksum += ((uint)((rawPacket.RawPacket[i] << 8) | rawPacket.RawPacket[i + 1]));
            checksum += 6;
            checksum += (rawPacket.RawPacketLength - 20);

            while (checksum >> 16 != 0)
                checksum = (checksum & 0xffff) + (checksum >> 16);


            checksum = ~checksum;
            var result = (ushort)checksum;
            rawPacket.RawPacket[36] = (byte)(result >> 8);
            rawPacket.RawPacket[37] = (byte)result;
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
            return (destPort == 80 || destPort == 8082);
        }

        private bool IsIn(Packet rawPacket)
        {
            return !IsOut(rawPacket);
        }

        [Flags]
        private enum State
        {
            Closed = 1 << 0,
            Connected = 1 << 1,
            Transferring = 1 << 2,
            Caching = 1 << 3,
            SendingCache = 1 << 4,
            WaitResponse = 1 << 5,
            MISSEDPACKETS = 1 << 6
        }
    }
}