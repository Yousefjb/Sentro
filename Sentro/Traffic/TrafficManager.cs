using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsipility : Sniff HTTP/S Request and Respons packets and take action
        TODO: refactor divert                  
    */    

    public class TrafficManager
    {
        private const string Tag = "TrafficManager";
        private static TrafficManager _trafficManager;              
        private static FileLogger _fileLogger;
        private static CacheManager _cacheManager;        
        private bool _running;               

        public TrafficManager()
        {
            _cacheManager = CacheManager.GetInstance();            
            _fileLogger = FileLogger.GetInstance();            
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }

        private delegate bool IsIncomePacket(Address address, IPAddress ipAddress);

        private void Divert(IsIncomePacket isIncomePacket,DivertLayer layer,string filter)
        {
            var divertDict = new Dictionary<Connection, ConnectionBuffer>();
            Diversion diversion;

            try
            {
                diversion = Diversion.Open(filter, layer, 100, 0);
                diversion.SetParam(DivertParam.QueueLength, 4096);
                diversion.SetParam(DivertParam.QueueTime, 1024);                
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag, e.ToString());
                return;
            }

            if (!diversion.Handle.Valid)
            {
                _fileLogger.Error(Tag, $"Failed to open divert handle with error {Marshal.GetLastWin32Error()}");
                return;
            }

            var address = new Address();

            var buffer = new byte[4096];

            var ipHeader = new IPHeader();
            var tcpHeader = new TCPHeader();


            while (_running)
            {

                uint receiveLength = 0;
                uint sendLength = 0;

                if (!diversion.Receive(buffer, address, ref receiveLength))
                {
                    _fileLogger.Error(Tag, $"Failed to receive packet with error {Marshal.GetLastWin32Error()}");
                    continue;
                }

                diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                var packet = new Packet(buffer, receiveLength, tcpHeader, ipHeader);
                try
                {
                    var connection = new Connection(packet.SourceIp.ToString(), packet.SourcePort,
                        packet.DestinationIp.ToString(), packet.DestinationPort);
                    
                    /*if already started caching*/
                    if (divertDict.ContainsKey(connection))
                    {                        
                        //if(address.Direction == DivertDirection.Inbound)
                            //diversion.SendAsync(buffer, receiveLength, address, ref sendLength);                        

                        /*pass - no need to calcualte checksum*/
                        if (!divertDict[connection].LockedForCache || (DateTime.Now.Millisecond - divertDict[connection].LockedAt) > 100000)
                        {
                            divertDict[connection].LockedAt = 0;
                            diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                            
                            if (isIncomePacket.Invoke(address, packet.DestinationIp))
                            {
                                if (packet.DataLength > 0)
                                {
                                    var connectionBuffer = divertDict[connection];

                                    connectionBuffer.AddResponsePacket(packet);
                                    
                                    /*resposne not complete yet*/
                                    if (connectionBuffer.ResponseCompleted)
                                    {                                        
                                        divertDict.Remove(connection);
                                    }
                                }
                            }
                                
                            /*end of http response*/
                        }             
                    }
                    else // new connection need to moniture
                    {
                        if (isIncomePacket.Invoke(address,packet.DestinationIp))
                        {
                            /*
                          new connection started as inbound is either 
                          due to missed request or sentro started after request submit.
                          let it pass
                         */
                            diversion.SendAsync(buffer, receiveLength, address, ref sendLength);                            
                        }
                        else
                        {
                            var request = new SentroRequest(packet);
                            if (!request.IsValid)
                            {                               
                                diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                            }
                            else //cacheable
                            {                                
                                //dor debug//diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                                var cacheResponse = _cacheManager.Get(request);
                                if (cacheResponse != null)
                                {                                    
                                    divertDict.Add(connection,new ConnectionBuffer(null) {LockedForCache = true,LockedAt = DateTime.Now.Millisecond});
                                    address.Direction = DivertDirection.Outbound;
                                    var random = (ushort) DateTime.Now.Millisecond;                                    
                                    
                                    var seq = request.Packet.TcpHeader.AcknowledgmentNumber.Reverse();
                                    var ack = request.Packet.TcpHeader.SequenceNumber.Reverse()
                                              + (uint) request.Packet.DataLength;

                                    var address2 = new Address
                                    {
                                        Direction = address.Direction,
                                        InterfaceIndex = address.InterfaceIndex,
                                        SubInterfaceIndex = address.SubInterfaceIndex
                                    };
                                    //Task.Run(() =>
                                    //{
                                        foreach (var p in cacheResponse.NetworkPackets)
                                        {
                                            SetFakeHeaders(p, request.Packet, random++, seq, ack, 0);
                                            diversion.CalculateChecksums(p.RawPacket, p.RawPacketLength, 0);
                                            diversion.SendAsync(p.RawPacket, p.RawPacketLength, address2, ref sendLength);
                                            seq += p.RawPacketLength - 40;
                                        }

                                        var fin = new Packet(new byte[40], 40, null, null);
                                        SetFakeHeaders(fin, request.Packet, random, seq, ack, 1);
                                        diversion.CalculateChecksums(fin.RawPacket, fin.RawPacketLength, 0);
                                        diversion.SendAsync(fin.RawPacket, fin.RawPacketLength, address2, ref sendLength);
                                        cacheResponse.Close();
                                    //});
                                }
                                else
                                {
                                    /*maybe swap these ? better performace but less reliable*/
                                    divertDict.Add(connection, new ConnectionBuffer(request));
                                    diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _fileLogger.Error(Tag, e.ToString());
                }

                /*keep working*/
            }

            diversion.Close();
        }

        public void Stop()
        {
            _running = false;                       
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            new Thread(DivertForwardMode).Start();
            new Thread(DivertNormalMode).Start();
        }

        private void DivertForwardMode()
        {
            Divert(ForwardDiverser.IsIncomePacket,DivertLayer.NetworkForward,ForwardDiverser.DefaultFilter);
        }

        private void DivertNormalMode()
        {
            Divert(Diverser.IsIncomePacket, DivertLayer.Network, Diverser.DefaultFilter);
        }
     
        private void SetFakeHeaders(Packet response,Packet request,ushort random,uint seq,uint ack,byte fin)
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
            var srcIp = request.IpHeader.DestinationAddress.GetAddressBytes();                        
            response.RawPacket[12] = srcIp[0];
            response.RawPacket[13] = srcIp[1];
            response.RawPacket[14] = srcIp[2];
            response.RawPacket[15] = srcIp[3];

            //Destination Ip address 4 bytes
            var desIp = request.IpHeader.SourceAddress.GetAddressBytes();   
            //For debuging : put in decreasing order 3 -> 0
            response.RawPacket[16] = desIp[0];
            response.RawPacket[17] = desIp[1];
            response.RawPacket[18] = desIp[2];
            response.RawPacket[19] = desIp[3];

            // END OF IP HEADERS

            //Source Port 2 bytes
            //reverse
            var srcPort = BitConverter.GetBytes(request.TcpHeader.DestinationPort);
            response.RawPacket[20] = srcPort[1];
            response.RawPacket[21] = srcPort[0];

            //Destination Port 2 bytes
            //reverse
            var dstPort = BitConverter.GetBytes(request.TcpHeader.SourcePort);
            response.RawPacket[22] = dstPort[1];
            response.RawPacket[23] = dstPort[0];
            
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

            //flags are ack (16) and psh (8) = 24 and rst (4)
            response.RawPacket[33] = (byte)(16 + fin);

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
    }
}
