using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Divert.Net;
using Sentro.ARP;
using Sentro.Utilities;
using Sentro.Cache;

namespace Sentro.Traffic
{
    /*
        Responsipility : Sniff HTTP/S Request and Respons packets and take action
    */
    //TODO handle packets in async 
    //TODO clean up

    public class TrafficManager
    {
        public const string Tag = "TrafficManager";
        private static TrafficManager _trafficManager;
        private bool _running = false;
        private ConsoleLogger _logger;
        private CacheManager _cacheManager;
        private ArpSpoofer _arpSpoofer;

        private TrafficManager()
        {
            _logger = ConsoleLogger.GetInstance();
            _cacheManager = CacheManager.GetInstance();     
            _arpSpoofer = ArpSpoofer.GetInstance();              
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }

        public void Start()
        {
            if(_running)
                return;

            _running = true;
            Task.Run(() => Divert(true));
            Task.Run(() => Divert(false));
        }


        private void Divert(bool forwardMode)
        {
            var divertDict = new Dictionary<Connection, ConnectionBuffer>();
            Diversion diversion;

            string filter = "tcp.DstPort == 80 or tcp.SrcPort == 80";
            //string filter = "tcp.PayloadLength > 0 and (tcp.DstPort == 80 or tcp.DstPort == 443 or tcp.SrcPort == 443 or tcp.SrcPort == 80)";

            try
            {
                diversion = Diversion.Open(filter, forwardMode ? DivertLayer.NetworkForward : DivertLayer.Network, 100,0);
            }
            catch (Exception e)
            {
                _logger.Error(Tag, e.Message);
                return;
            }

            if (!diversion.Handle.Valid)
            {
                _logger.Info(Tag,
                    $"Failed to open divert handle with error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return;
            }

            Address address = new Address();

            byte[] buffer = new byte[65535];

            uint receiveLength, sendLength;            
            FileStream file = new FileStream("sentro.txt", FileMode.Create);            

            IPHeader ipHeader = new IPHeader();
            TCPHeader tcpHeader = new TCPHeader();

            _logger.Info(Tag,"started traffic manager");
            while (_running)
            {
                receiveLength = 0;
                sendLength = 0;

                if (!diversion.Receive(buffer, address, ref receiveLength))
                {
                    _logger.Info(Tag,
                        $"Failed to receive packet with error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                    continue;
                }

                var connection = new Connection(ipHeader.SourceAddress.ToString(), tcpHeader.SourcePort,
                        ipHeader.DestinationAddress.ToString(), tcpHeader.DestinationPort);


                if (divertDict.ContainsKey(connection)) //already started caching
                {
                    /*pass - no need to calcualte checksum*/                   
                    diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                   
                    if(address.Direction == DivertDirection.Inbound ||
                        (forwardMode && IsArpTarget(ipHeader.DestinationAddress.ToString())))
                    {
                        diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);

                        var connectionBuffer = divertDict[connection];
                        connectionBuffer.Buffer(ref buffer, (int) receiveLength);
                        
                        /*end of http response*/
                        if (tcpHeader.Psh == 1)
                        {                            
                            var offset = Offset(tcpHeader, ipHeader);

                            if (receiveLength - offset > 0
                                && IsHttpResponse(ref buffer, offset,receiveLength))
                            {
                                var request = connectionBuffer.Request();
                                var response = connectionBuffer.Response();
                                _cacheManager.Cache(request,response);
                                connectionBuffer.Reset();                                
                                divertDict.Remove(connection);
                            }
                        }                                
                    }       
                }
                else // new connection need to moniture
                {
                    if (address.Direction == DivertDirection.Inbound ||
                       (forwardMode && IsArpTarget(ipHeader.DestinationAddress.ToString())))
                    {
                        /*
                          new connection started as inbound is either 
                          due to missed request or sentro started after request submit
                          let it pass
                        */
                        diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                    }
                    else
                    {
                        //detect non cacheable and incomplete
                        var offset = Offset(tcpHeader, ipHeader);
                        var strangePacket = receiveLength - offset <= 0;
                        if (tcpHeader.Psh == 0 || strangePacket || !IsHttpGet(ref buffer,offset,receiveLength))
                        {
                            diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                        }
                        else //cacheable
                        {                            
                            var request = new SentroRequest(ref buffer,(int)receiveLength);
                            var response = _cacheManager.Get(request);
                            if (response != null)
                            {   
                                response.MatchFor(request);
                                var packets = response.Packets();                                  
                                address.Direction = DivertDirection.Outbound;
                                foreach (var packet in packets)
                                {
                                    diversion.CalculateChecksums(packet, (uint) packet.Length, 0);
                                    diversion.SendAsync(packet, (uint) packet.Length, address, ref sendLength);
                                }
                                /*reset connection*/                                
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


                if (address.Direction == DivertDirection.Outbound)
                {
                    diversion.CalculateChecksums(buffer, receiveLength, 0);
                }
                diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                asyncParseAndLog(receiveLength,file,diversion,buffer,ipHeader,tcpHeader);               
            }


            diversion.Close();
        }


        private static int Offset(TCPHeader tcpHeader, IPHeader ipHeader)
        {
            if (tcpHeader == null || ipHeader == null)
                return 0;

            return tcpHeader.HeaderLength*4 + ipHeader.HeaderLength*4;
        }

        private static bool IsHttpGet(ref byte[] packetBytes, int offset,uint length)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset,(int)length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpGet);
        }

        private static bool IsHttpResponse(ref byte[] packetBytes, int offset,uint length)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset, (int)length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpResonse);
        }

        private bool IsArpTarget(string ip)
        {
            return _arpSpoofer.State() != ArpSpoofer.Status.Stopped && _arpSpoofer.IsTargeted(ip);
        }

        /*should be moved to logger*/
        private void Log(string o, FileStream file)
        {
            //Console.WriteLine(o);
            o = o + "\n";
            var bytes = Encoding.ASCII.GetBytes(o);
            file.Write(bytes, 0, bytes.Length);
        }
        /*should be moved to logger*/
        private void Log(byte[] o, int start, int count, FileStream file)
        {
            //Console.WriteLine(o);                        
            file.Write(o, start, count);
        }

        public void Stop()
        {
            _running = false;
        }        

       
        /*testing function*/
        private async void asyncParseAndLog(uint receiveLength, FileStream file, Diversion diversion, byte[] buffer,
          IPHeader ipHeader, TCPHeader tcpHeader)
        {
            await Task.Run(() =>
            {
                diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                var start = ipHeader.HeaderLength * 4 + tcpHeader.HeaderLength * 4;
                var count = receiveLength - start;

                if (count <= 0)
                    return;

                Log("tcp header length : " + tcpHeader.HeaderLength + " \n ip header length : " + ipHeader.HeaderLength, file);
                Log($"ip : {ipHeader.SourceAddress} -> {ipHeader.DestinationAddress} \n port : {tcpHeader.SourcePort} -> {tcpHeader.DestinationPort}", file);
                Log(buffer, start, (int)count, file);
            });
        }


    }
}
