using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Divert.Net;
using Sentro.ARP;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsipility : Sniff HTTP/S Request and Respons packets and take action
    */
    //TODO handle packets in async 
    //TODO where should i use CalcualteChecksum ?
    //TODO clean up

    public class TrafficManager
    {
        public const string Tag = "TrafficManager";
        private static TrafficManager _trafficManager;
        private bool _running;        
        private FileLogger _flogger;
        private CacheManager _cacheManager;
        private ArpSpoofer _arpSpoofer;
        private Diversion _diversionNormal, _diversionForward;

        private TrafficManager()
        {            
            _cacheManager = CacheManager.GetInstance();
            _arpSpoofer = ArpSpoofer.GetInstance();
            _flogger = FileLogger.GetInstance();
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }

        private void Divert(bool forwardMode)
        {
            var divertDict = new Dictionary<Connection, ConnectionBuffer>();
            Diversion diversion = forwardMode ? _diversionForward : _diversionNormal;
            string filter = "tcp.DstPort == 80 or tcp.SrcPort == 80";
            //string filter = "tcp.PayloadLength > 0 and (tcp.DstPort == 80 or tcp.DstPort == 443 or tcp.SrcPort == 443 or tcp.SrcPort == 80)";

            try
            {
                diversion = Diversion.Open(filter, forwardMode ? DivertLayer.NetworkForward : DivertLayer.Network, 100,
                    0);
                diversion.SetParam(DivertParam.QueueLength, 4096);
                diversion.SetParam(DivertParam.QueueTime, 1024);
            }
            catch (Exception e)
            {
                _flogger.Error(Tag, e.Message);
                return;
            }

            if (!diversion.Handle.Valid)
            {
                //_flogger.Debug(Tag,
                //    $"Failed to open divert handle with error {Marshal.GetLastWin32Error()}");
                return;
            }

            Address address = new Address();

            byte[] buffer = new byte[65535];

            uint receiveLength, sendLength;

            IPHeader ipHeader = new IPHeader();
            TCPHeader tcpHeader = new TCPHeader();

            try
            {                
                while (_running)
                {
                    receiveLength = 0;
                    sendLength = 0;

                    if (!diversion.Receive(buffer, address, ref receiveLength))
                    {
                        //_flogger.Debug(Tag,
                        //    $"Failed to receive packet with error {Marshal.GetLastWin32Error()}");
                        continue;
                    }

                    diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);                    

                    var connection = new Connection(ipHeader.SourceAddress.ToString(), tcpHeader.SourcePort,
                        ipHeader.DestinationAddress.ToString(), tcpHeader.DestinationPort);

                    if (divertDict.ContainsKey(connection)) //already started caching
                    {

                        /*pass - no need to calcualte checksum*/
                        diversion.SendAsync(buffer, receiveLength, address, ref sendLength);

                        if (address.Direction == DivertDirection.Inbound ||
                            (forwardMode && IsArpTarget(ipHeader.DestinationAddress.ToString())))
                        {
                            var connectionBuffer = divertDict[connection];

                            connectionBuffer.Buffer(ref buffer, (int) receiveLength);
                            /*end of http response*/
                            if (tcpHeader.Psh == 1)
                            {
                                var offset = HelperFunctions.Offset(tcpHeader, ipHeader);

                                if (receiveLength - offset > 0
                                    && HelperFunctions.IsHttpResponse(buffer, offset, receiveLength))
                                {
                                    var request = connectionBuffer.Request();
                                    var response = connectionBuffer.Response();
                                    _cacheManager.Cache(request, response);
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
                          due to missed request or sentro started after request submit.
                          let it pass
                         */
                            diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                        }
                        else
                        {
                            //detect non cacheable and incomplete
                            var offset = HelperFunctions.Offset(tcpHeader, ipHeader);
                            var strangePacket = receiveLength - offset <= 0;
                            if (tcpHeader.Psh == 0 || strangePacket || !HelperFunctions.IsHttpGet(buffer, offset, receiveLength))
                            {
                                diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                            }
                            else //cacheable
                            {
                                var request = new SentroRequest(buffer, (int) receiveLength);
                                var response = _cacheManager.Get(request);
                                if (response != null)
                                {
                                    response.SetAddressesFrom(request);
                                    var responsePackets = response.Packets();
                                    address.Direction = DivertDirection.Outbound;
                                    foreach (var packet in responsePackets)
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
                }
            }
            catch (Exception e)
            {
                _flogger.Error(Tag, e.ToString());
            }

            diversion.Close();
        }

        public void Parse(byte[] bytes, uint legnth,out TCPHeader tcpHeader,out IPHeader ipHeader)
        {
            var tcp = new TCPHeader();
            var ip = new IPHeader();
            _diversionNormal?.ParsePacket(bytes, legnth, ip, null, null, null, tcp, null);
            tcpHeader = tcp;
            ipHeader = ip;
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
            //Task.Run(() => Divert(true));
            Task.Run(() => Divert(false));            
        }

        private bool IsArpTarget(string ip)
        {
            return _arpSpoofer.State() != ArpSpoofer.Status.Stopped && _arpSpoofer.IsTargeted(ip);
        }

    }
}
