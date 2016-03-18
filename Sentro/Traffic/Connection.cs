using System;
using System.Collections.Concurrent;
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
        private readonly Address address;
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

        public Connection(Diversion diversion,Address address)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;
            this.address = address;
            timeoutTimer = new System.Timers.Timer();
            timeoutTimer.Elapsed += OnTimeout;
            timeoutTimer.Interval = 30*1000;
            timeoutTimer.Enabled = true;
            fileLogger = FileLogger.GetInstance();      
            fileLogger.Debug(Tag,"new connection");            
        }

        private void OnTimeout(object source,ElapsedEventArgs e)
        {                        
            fileLogger.Debug(Tag,"timeout");
            CurrentState = State.Closed;
            ClearResources();
        }

        public void ClearResources()
        {
            if (cachingFileStream != null)
            {
                cachingFileStream.Flush();
                cachingFileStream.Close();
                cachingFileStream.Dispose();
            }

            queuedPackets = null;
            if (CurrentState.HasFlag(State.Caching))
                CacheManager.Delete(HashCode);
            Connection c;
            KvStore.Connections.TryRemove(HashCode, out c);
            fileLogger.Debug(Tag, "cleared resources");
        }

        private void resetTimer(Packet rawPacket)
        {
            timeoutTimer.Stop();
            timeoutTimer.Start();
            fileLogger.Debug(Tag, $"{rawPacket.SrcIp}:{rawPacket.SrcPort}->{rawPacket.DestIp}:{rawPacket.DestPort}");
        }

        public void Add(Packet rawPacket)
        {
            resetTimer(rawPacket);

            if (CurrentState.HasFlag(State.Established))
                Established(rawPacket);

            else if (CurrentState.HasFlag(State.Caching))
                Caching(rawPacket);

            else if (CurrentState.HasFlag(State.SendingCache))
                SendingCache(rawPacket);

            else if (CurrentState.HasFlag(State.Establishing))
                Establishing(rawPacket);

            else if (CurrentState.HasFlag(State.Closing))
                Closing(rawPacket);

            else if (CurrentState.HasFlag(State.Closed))
                Closed(rawPacket);

            else if (CurrentState.HasFlag(State.SentCache))
                SentCache(rawPacket);

            else if (CurrentState.HasFlag(State.OutOfControl))
                SendAsync(rawPacket);
        }

        private byte _windowScale;

        private void Closed(Packet rawPacket)
        {

            if (rawPacket.Syn)
            {
                CurrentState = State.Establishing;
                _windowScale = rawPacket.WindowScale;
                fileLogger.Debug(Tag, "closed:syn -> establishing");
            }
            else if (rawPacket.SynAck || rawPacket.Ack)
            {
                CurrentState = State.Established;
                fileLogger.Debug(Tag, "closed:ack|synack -> established");
            }
            else if (rawPacket.Fin || rawPacket.FinAck)
            {
                fileLogger.Debug(Tag, "closed:fin|finack -> closed");
            }
            SendAsync(rawPacket);
        }

        private void Establishing(Packet rawPacket)
        {
            if (rawPacket.Ack)
            {
                CurrentState = State.Established;
                fileLogger.Debug(Tag, "establishing:ack -> established");
            }
            else if (rawPacket.Fin)
            {
                CurrentState = State.Closing;
                fileLogger.Debug(Tag, "establishing:fin -> closing");
            }
            else if (rawPacket.FinAck)
            {
                CurrentState = State.Closed;
                fileLogger.Debug(Tag, "establishing:finack -> closed");
            }
            SendAsync(rawPacket);
        }

        private void Established(Packet rawPacket)
        {            
            var destPort = rawPacket.DestPort;

            if (rawPacket.Fin)
            {
                CurrentState = State.Closing;
                fileLogger.Debug(Tag, "established:fin -> closing");
            }
            else if (rawPacket.Rst)
            {
                CurrentState = State.Closed;
                fileLogger.Debug(Tag, "established:rst -> closed");
            }
            else if (rawPacket.Syn)
            {
                CurrentState = State.Establishing;

                fileLogger.Debug(Tag, "established:syn -> establishing");
            }
            if (destPort == 80 || destPort == 443)
            {
                if (rawPacket.DataLength == 0)
                {
                    fileLogger.Debug(Tag, "established: empty packet");
                    SendAsync(rawPacket);
                }
                else if (rawPacket.IsHttpGet())
                {
                    fileLogger.Debug(Tag, $"established:uri -> {rawPacket.Uri}");
                    fileLogger.Debug(Tag, $"established:lastGet -> {hashOfLastHttpGet}");
                    hashOfLastHttpGet = rawPacket.Uri.NormalizeUri().MurmurHash();
                    fileLogger.Debug(Tag, $"established:newlastGet -> {hashOfLastHttpGet}");
                    if (CacheManager.IsCached(hashOfLastHttpGet))
                    {
                        fileLogger.Debug(Tag, "established:isCached -> true");
                        //if (CacheManager.ShouldValidiate(hashOfLastHttpGet))
                        //    Validate();
                        var response = CacheManager.Get(hashOfLastHttpGet);
                        CurrentState = State.SendingCache;
                        SendCacheResponse(response, rawPacket);
                    }
                    else
                    {
                        fileLogger.Debug(Tag, "established:isCached -> false");
                        SendAsync(rawPacket);
                    }
                }
                else
                {
                    fileLogger.Debug(Tag, "established:httpGet -> NO");
                    SendAsync(rawPacket);
                    hashOfLastHttpGet = "";
                }
            }
            else
            {

                if (CurrentState.HasFlag(State.Transmitting))
                {
                    fileLogger.Debug(Tag, "established:transmitting -> yes");
                    SendAsync(rawPacket);
                }
                else if (rawPacket.DataLength == 0)
                {
                    fileLogger.Debug(Tag, "established:income -> empty");
                    SendAsync(rawPacket);
                }
                else if (rawPacket.IsHttpResponse())
                {
                    if (hashOfLastHttpGet.Length == 0)
                    {
                        fileLogger.Debug(Tag, "established:response -> post or not get");
                    }
                    else if (CacheManager.IsCacheable(rawPacket.HttpResponseHeaders))
                    {
                        fileLogger.Debug(Tag, "established:httpResp -> cachable");
                        CurrentState = State.Caching;
                        fileLogger.Debug(Tag, "established:isCacheable -> Caching");
                        Caching(rawPacket);
                        fileLogger.Debug(Tag, "established:isHttpResponse -> returned from caching call");
                    }
                    else
                    {
                        fileLogger.Debug(Tag, "established:isHttpResponse -> not cachable");
                        CurrentState = State.Established | State.Transmitting;
                        fileLogger.Debug(Tag, "established:not cachable -> established | transmitting");
                        SendAsync(rawPacket);
                    }
                }
                else
                {
                    fileLogger.Debug(Tag, "established:ishttpResp -> No");
                }
            }
        }

        private void Closing(Packet rawPacket)
        {
            SpinWait.SpinUntil(() => WritingPacket == false);
            if (rawPacket.Ack)
            {
                CurrentState = State.Closed;
                fileLogger.Debug(Tag, "closing:ack -> closed");
            }
            else if (rawPacket.Syn)
            {
                CurrentState = State.Establishing;
                fileLogger.Debug(Tag, "closing:syn -> establishing");
            }
            else if (rawPacket.SynAck)
            {
                CurrentState = State.Established;
                fileLogger.Debug(Tag, "closing:synack -> established");
            }
            else
            {
                CurrentState = State.OutOfControl;
                fileLogger.Debug(Tag, "closing:? -> outofcontrol");
            }
            SendAsync(rawPacket);
        }

        //Let this be the last one
        private FileStream cachingFileStream;
        private Dictionary<uint, Packet> queuedPackets;        
        private int responseContentLength;
        private uint expectedSequence;
        private int cachedContentLength;
        private void Caching(Packet rawPacket)
        {            
            /*
            this state start with the first packet sent from server 
            which contains the http response headers as well            
            */
            SendAsync(rawPacket);
            if (cachingFileStream == null || queuedPackets == null)
            {
                cachingFileStream = CacheManager.OpenFileWriteStream(hashOfLastHttpGet);
                queuedPackets = new Dictionary<uint, Packet>();
                
                responseContentLength = rawPacket.HttpResponseHeaders.ContentLength;
                fileLogger.Debug(Tag, $"caching:#1 -> content length {responseContentLength}");
                expectedSequence = rawPacket.SeqNumber;
                fileLogger.Debug(Tag, $"caching:expectedSeq -> {expectedSequence}");
            }            
                

            var destPort = rawPacket.DestPort;

            //ack from user could be for multiple packets
            if (destPort == 80 || destPort == 443)
            {
                if (rawPacket.Fin)
                {
                    CurrentState = State.Closing;
                    fileLogger.Debug(Tag, "caching:fin -> closing");
                }
                else if (rawPacket.FinAck || rawPacket.Rst)
                {
                    CurrentState = State.Closed;
                    fileLogger.Debug(Tag, "caching:finack | rst -> closed");
                }
            }
            else
            {
                if (!queuedPackets.ContainsKey(rawPacket.SeqNumber))
                {
                    fileLogger.Debug(Tag, $"caching:seq not in dict -> {rawPacket.SeqNumber}");
                    queuedPackets.Add(rawPacket.SeqNumber, rawPacket);
                    TryWriteOrderedPackets();
                }
                else
                {
                    fileLogger.Debug(Tag, $"caching:seq in dict -> {rawPacket.SeqNumber}");
                }
            }         
        }

        private bool WritingPacket = false;

        private void TryWriteOrderedPackets()
        {            
            WritingPacket = true;
            while (queuedPackets.ContainsKey(expectedSequence))
            {
                Packet packet = queuedPackets[expectedSequence];
                queuedPackets.Remove(expectedSequence);                
                var packetLength = packet.DataLength;
                cachingFileStream.Write(packet.RawPacket, packet.DataStart, packetLength);
                cachingFileStream.Flush();
                expectedSequence += (uint) packetLength;
                cachedContentLength += packetLength;
            }

            fileLogger.Debug(Tag, $"orderpacket: missing -> {expectedSequence}");
            if (cachedContentLength == responseContentLength)
            {
                CurrentState = State.Established;
                fileLogger.Debug(Tag, "orderpacket:fullcache -> established");
                cachingFileStream.Close();
                cachingFileStream.Dispose();
            }
            else
            {
                fileLogger.Debug(Tag, "orderpacket:fullcache -> NO");
            }
            WritingPacket = false;
        }

        private void SendingCache(Packet rawPacket)
        {
            var destPort = rawPacket.DestPort;
            if (destPort == 80 || destPort == 443)
            {
                pauseSendingCache = (rawPacket.WindowSize*(1 << _windowScale)) < 3000;
                fileLogger.Debug(Tag,
                    $"sendingCache: paused : windowSize {rawPacket.WindowSize}-> {pauseSendingCache}");
                if (rawPacket.Fin || rawPacket.Rst)
                {
                    CurrentState = State.Closing;
                    fileLogger.Debug(Tag, "sendingCache:fin | rst -> closing");
                }
            }
        }

        private void SentCache(Packet rawPacket)
        {

            var destPort = rawPacket.DestPort;
            if (destPort == 80 || destPort == 443)
            {
                if (rawPacket.Fin || rawPacket.Rst)
                {
                    CurrentState = State.Closed;
                    fileLogger.Debug(Tag, "sentcache:fin | rst -> closed");
                }
                else if (rawPacket.Syn || rawPacket.SynAck)
                {
                    CurrentState = State.Establishing;
                    fileLogger.Debug(Tag, "sentcache:syn | synack -> establishing");
                }
                else if (rawPacket.DataLength > 0)
                {
                    fileLogger.Debug(Tag, $"sentcache:not empty -> {rawPacket.DataLength}");
                    if (rawPacket.IsHttpGet())
                    {
                        CurrentState = State.Established;
                        fileLogger.Debug(Tag, "sentcache:httpget -> call established");
                        Established(rawPacket);
                        fileLogger.Debug(Tag, "sentcache:httpget -> returned from call established");
                    }
                }
                else
                {
                    fileLogger.Debug(Tag, "sentcache:datalength -> 0");
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
                if (CurrentState.HasFlag(State.Closed) || CurrentState.HasFlag(State.Closing))
                {
                    fileLogger.Debug(Tag, "sendcacheasync:closed | closing -> break");
                    break;
                }

                SetFakeHeaders(packet, requestPacket, random++, seq, ack, 0);
                fileLogger.Debug(Tag, "sendcacheasync:fakeheaders -> set");
                SpinWait.SpinUntil(() => pauseSendingCache == false);
                fileLogger.Debug(Tag, "sendcacheasync:spin -> done");
                diversion.CalculateChecksums(packet.RawPacket, packet.RawPacketLength, 0);
                SendAsync(packet);
                seq += packet.RawPacketLength - 40;                
            }
            response.Close();
            CurrentState = State.SentCache;
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

        private void SendAsync(Packet rawPacket)
        {
            diversion.SendAsync(rawPacket.RawPacket, rawPacket.RawPacketLength, address, ref _sentLength);
        }

        private void Validate()
        {
            throw new NotImplementedException();
        }

        [Flags]
        private enum State
        {
            Established    = 1 << 0,
            Establishing   = 1 << 1, 
            Closed         = 1 << 2,
            Closing        = 1 << 3,             
            Caching        = 1 << 4,
            SentCache      = 1 << 5,
            SendingCache   = 1 << 6,
            OutOfControl   = 1 << 7,
            Transmitting   = 1 << 8
        }
    }
}
