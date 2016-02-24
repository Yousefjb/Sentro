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
        TODO handle packets in async 
        TODO where should i use CalcualteChecksum ?     
    */    

    public class TrafficManager
    {
        public const string Tag = "TrafficManager";
        private static TrafficManager _trafficManager;              
        private static FileLogger _fileLogger;
        private static CacheManager _cacheManager;
        private static ArpSpoofer _arpSpoofer;
        private Diversion _diversionNormal, _diversionForward;
        private bool _running;

        private const string Filter = "tcp.DstPort == 80 or tcp.SrcPort == 80";

        private TrafficManager()
        {            
            _cacheManager = CacheManager.GetInstance();
            _arpSpoofer = ArpSpoofer.GetInstance();
            _fileLogger = FileLogger.GetInstance();
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }

        private void Divert(bool forwardMode)
        {
            var divertDict = new Dictionary<Connection, ConnectionBuffer>();
            Diversion diversion = forwardMode ? _diversionForward : _diversionNormal;

            try
            {
                diversion = Diversion.Open(Filter, forwardMode ? DivertLayer.NetworkForward : DivertLayer.Network, 100,
                    0);
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
                _fileLogger.Error(Tag,
                    $"Failed to open divert handle with error {Marshal.GetLastWin32Error()}");
                return;
            }

            Address address = new Address();

            byte[] buffer = new byte[65535];

            uint receiveLength, sendLength;

            IPHeader ipHeader = new IPHeader();
            TCPHeader tcpHeader = new TCPHeader();


            while (_running)
            {

                receiveLength = 0;
                sendLength = 0;

                if (!diversion.Receive(buffer, address, ref receiveLength))
                {
                    _fileLogger.Error(Tag,
                        $"Failed to receive packet with error {Marshal.GetLastWin32Error()}");
                    continue;
                }

                Task.Factory.StartNew(() =>
                {
                    diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);

                    var connection = new Connection(ipHeader.SourceAddress.ToString(), tcpHeader.SourcePort,
                        ipHeader.DestinationAddress.ToString(), tcpHeader.DestinationPort);

                    /*if already started caching*/
                    if (divertDict.ContainsKey(connection))
                    {
                        /*pass - no need to calcualte checksum*/
                        diversion.SendAsync(buffer, receiveLength, address, ref sendLength);

                        if (address.Direction == DivertDirection.Inbound ||
                            (forwardMode && IsArpTarget(ipHeader.DestinationAddress.ToString())))
                        {
                            var connectionBuffer = divertDict[connection];

                            connectionBuffer.Buffer(ref buffer, (int) receiveLength);

                            /*resposne not complete yet*/
                            if (tcpHeader.Psh != 1) return;

                            /*end of http response*/
                            var offset = HelperFunctions.Offset(tcpHeader, ipHeader);

                            if (receiveLength - offset <= 0 ||
                                !HelperFunctions.IsHttpResponse(buffer, offset, receiveLength)) return;

                            var request = connectionBuffer.Request();
                            var response = connectionBuffer.Response();
                            _cacheManager.Cache(request, response);
                            connectionBuffer.Reset();
                            divertDict.Remove(connection);
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
                            var emptyPacket = receiveLength - offset <= 0;
                            if (tcpHeader.Psh == 0 || emptyPacket ||
                                !HelperFunctions.IsHttpGet(buffer, offset, receiveLength))
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
                }).ContinueWith(tsk =>
                {
                    _fileLogger.Error(Tag,tsk.Exception?.ToString());
                },TaskContinuationOptions.OnlyOnFaulted);

                /*keep working*/
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
