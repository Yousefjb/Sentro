using System;
using System.Threading;
using System.Threading.Tasks;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : Hold addresses for a connection
    */
    class Connection
    {
        public const string Tag = "Connection";
        public string SourceIp { get; }        
        public ushort SourcePort { get; }        
        public string DestinationIp { get; }        
        public ushort DestinationPort { get; }        
        public Connection(string sourceIp, ushort sourcePort, string destinationIp, ushort destinationPort)
        {
            SourceIp = sourceIp;            
            DestinationIp = destinationIp;
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
        }

        public State CurrentState = State.Closed;
        private Diversion diversion;
        private Address address;
        private bool pauseSendingCache;
        private string hashOfLastHttpGet = "";

        /// <summary>
        /// Control a connection between user and router
        /// </summary>
        /// <param name="diversion">used to pass/send packets on need 
        /// </param><param name="address">used to identify interface number    
        /// </param>
        public Connection(Diversion diversion,Address address)
        {
            this.diversion = diversion;
            CurrentState = State.Closed;
            this.address = address;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Connection))
                return false;
            Connection con = (Connection)obj;

            bool result = ((con.SourceIp.Equals(SourceIp)) && (con.SourcePort == SourcePort) && (con.DestinationIp.Equals(DestinationIp)) && (con.DestinationPort == DestinationPort)) ||
                ((con.SourceIp.Equals(DestinationIp)) && (con.SourcePort == DestinationPort) && (con.DestinationIp.Equals(SourceIp)) && (con.DestinationPort == SourcePort));

            return result;
        }

        public override int GetHashCode()
        {
            return ((SourceIp.GetHashCode() ^ SourcePort.GetHashCode()) as object).GetHashCode() ^
                ((DestinationIp.GetHashCode() ^ DestinationPort.GetHashCode()) as object).GetHashCode();
        }

        public override string ToString()
        {
            return $"{SourceIp}:{SourcePort} -> {DestinationIp}:{DestinationPort}";
        }

        private uint sentLength = 0;

        /// <summary>
        /// Add packet to connection which will take care of it by either
        /// send as is
        /// modify and send        
        /// block
        /// </summary>        
        /// <param name="rawPacket">packet which contains ip, tcp and data        
        /// </param>
        public void Add(Packet rawPacket)
        {
            if (CurrentState.HasFlag(State.Established))
                Established(rawPacket);

            else if (CurrentState.HasFlag(State.Caching))
                Caching(rawPacket);

            else if (CurrentState.HasFlag(State.SendingCache))
                SendingCache(rawPacket);                            

            else if (CurrentState.HasFlag(State.Cached))
                Cached(rawPacket);

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

        private async void Closed(Packet rawPacket)
        {
            SendAsync(rawPacket);
            if (rawPacket.Syn)
                CurrentState = State.Establishing;
            else if (rawPacket.SynAck)
                CurrentState = State.Established;
            else
                CurrentState = State.OutOfControl;
        }

        private async void Establishing(Packet rawPacket)
        {            
            SendAsync(rawPacket);
            if(rawPacket.Ack)
                CurrentState = State.Established;
            else if(rawPacket.Fin)
                CurrentState =  State.Closing;
            else if (rawPacket.FinAck)
                CurrentState = State.Closed;
            else
                CurrentState = State.OutOfControl;
        }

        private async void Established(Packet rawPacket)
        {
            var destPort = rawPacket.DestPort;

            if (rawPacket.Fin)
                CurrentState = State.Closing;
            else if (rawPacket.Rst)
                CurrentState = State.Closed;
            else if (rawPacket.Syn)
                CurrentState = State.OutOfControl;
                          
            if (destPort == 80 || destPort == 443)
            {
                if(rawPacket.DataLength == 0)
                    SendAsync(rawPacket);
                else if (rawPacket.IsHttpGet())
                {
                    hashOfLastHttpGet = rawPacket.Uri.NormalizeUri().MurmurHash();
                    if (await CacheManager.IsCached(hashOfLastHttpGet))
                    {
                        //if (CacheManager.ShouldValidiate(hashOfLastHttpGet))
                        //    Validate();
                        var response = await CacheManager.Get(hashOfLastHttpGet);
                        CurrentState = State.SendingCache;
                        SendCacheResponseAsync(response,rawPacket);
                    }
                    else
                        SendAsync(rawPacket);
                }
                else
                    SendAsync(rawPacket);                                
            }
            else
            {
                if (CurrentState.HasFlag(State.Transmitting))
                    SendAsync(rawPacket);
                else if (rawPacket.DataLength == 0)
                    SendAsync(rawPacket);
                else if (rawPacket.IsHttpResponse())
                {
                    if (await CacheManager.IsCacheable(rawPacket.HttpResponseHeaders))
                    {
                        CurrentState = State.Caching;
                        Caching(rawPacket);
                    }
                    else
                    {
                        CurrentState = State.Established | State.Transmitting;
                        SendAsync(rawPacket);
                    }
                }
            }
        }            

        private async void Closing(Packet rawPacket)
        {            
            SendAsync(rawPacket);
            if(rawPacket.Ack)
                CurrentState = State.Closed;
            else if(rawPacket.Syn)
                CurrentState = State.Establishing;
            else if (rawPacket.SynAck)
                CurrentState = State.Established;
            else
                CurrentState = State.OutOfControl;            
        }

        private async void Caching(Packet rawPacket)
        {
        }

        private async void Cached(Packet rawPacket)
        {

        }

        private async void SendingCache(Packet rawPacket)
        {
        }

        private async void SentCache(Packet rawPacket)
        {

        }

        private async void SendCacheResponseAsync(CacheResponse response, Packet requestPacket)
        {
            await Task.Run(() =>
            {
                var random = (ushort)DateTime.Now.Millisecond;

                var seq = requestPacket.AckNumber;
                var ack = requestPacket.SeqNumber + (uint)requestPacket.DataLength;

                foreach (var packet in response.NetworkPackets)
                {
                    SetFakeHeaders(packet, requestPacket, random++, seq, ack, 0);
                    SpinWait.SpinUntil(() => pauseSendingCache == false);
                    SendAsync(packet);
                    seq += packet.RawPacketLength - 40;
                }
            });
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
            var length = BitConverter.GetBytes((ushort)response.RawPacketLength);
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
            //For debuging : put in decreasing order 15 -> 12
            response.RawPacket[16] = request.RawPacket[12];
            response.RawPacket[17] = request.RawPacket[13];
            response.RawPacket[18] = request.RawPacket[14];
            response.RawPacket[19] = request.RawPacket[15];

            // END OF IP HEADERS

            var tcpStart = (request.RawPacket[0] & 15)*5;
            
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
            response.RawPacket[33] = (byte)(24 + fin);

            //windows size 2 bytes
            //set 16383 as defualt
            var windowSize = BitConverter.GetBytes((ushort)16383);
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
            diversion.SendAsync(rawPacket.RawPacket, rawPacket.RawPacketLength, address, ref sentLength);
        }

        private void Validate()
        {
            throw new NotImplementedException();
        }

        [Flags]
        public enum State
        {
            Established    = 1 << 0,
            Establishing   = 1 << 1, 
            Closed         = 1 << 2,
            Closing        = 1 << 3, 
            Cached         = 1 << 4,
            Caching        = 1 << 5,
            SentCache      = 1 << 6,
            SendingCache   = 1 << 7,
            OutOfControl   = 1 << 8,
            Transmitting   = 1 << 9
        }
    }
}
