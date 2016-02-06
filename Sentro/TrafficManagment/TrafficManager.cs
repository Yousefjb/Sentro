using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.TrafficManagment
{
    /*
        Responsipility : Sniff HTTP/S Request and Respons packets and take action
    */

    public class TrafficManager
    {
        public const string Tag = "TrafficManager";
        private static TrafficManager _trafficManager;
        private bool running = true;
        private ConsoleLogger logger;

        private TrafficManager()
        {
            logger = ConsoleLogger.GetInstance();
            //var divertForward = Task.Run(() => Divert(true));
            //var divertNetwork = Task.Run(() => Divert(false));    
            try
            {
                Task.Run(() => Divert(false));
            }
            catch (Exception e)
            {
                logger.Error(Tag, e.Message);
                logger.Error(Tag, e.StackTrace);
            }
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }

        public void Stop()
        {
            running = false;
        }


        private void Divert(bool forwardMode)
        {
            Dictionary<Connection, TcpRecon> divertDict = new Dictionary<Connection, TcpRecon>();
            Diversion diversion;

            string filter = "tcp.PayloadLength > 0 and (tcp.DstPort == 80 or tcp.SrcPort == 80)";
            //string filter = "tcp.PayloadLength > 0 and (tcp.DstPort == 80 or tcp.DstPort == 443 or tcp.SrcPort == 443 or tcp.SrcPort == 80)";

            try
            {
                diversion = Diversion.Open(filter, forwardMode ? DivertLayer.NetworkForward : DivertLayer.Network, 100,
                    0);
            }
            catch (Exception e)
            {
                logger.Error(Tag, e.Message);
                return;
            }

            if (!diversion.Handle.Valid)
            {
                logger.Info(Tag,
                    $"Failed to open divert handle with error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return;
            }

            Address address = new Address();

            byte[] buffer = new byte[65535];

            uint receiveLength, sendLength;
            ulong index = 0;
            FileStream file = new FileStream("sentro.txt", FileMode.Create);
            string o;

            IPHeader ipHeader = new IPHeader();
            TCPHeader tcpHeader = new TCPHeader();

            while (running)
            {
                receiveLength = 0;
                sendLength = 0;

                if (!diversion.Receive(buffer, address, ref receiveLength))
                {
                    logger.Info(Tag,
                        $"Failed to receive packet with error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                    continue;
                }


                /*
                if is outbound ie: request
                    parse
                    if available in cache
                        make a proper response from cache
                    else
                        let it pass        
                        put it in dictonary

                    
                else if inbound ie: response
                    let it pass
                    parse 
                    send the request and response to cache
                
                */

                if (address.Direction == DivertDirection.Inbound)
                {
                    diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                    diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                    Connection c = new Connection(ipHeader.SourceAddress.ToString(), tcpHeader.SourcePort,
                        ipHeader.DestinationAddress.ToString(), tcpHeader.DestinationPort);

                    if (!divertDict.ContainsKey(c))
                    {
                        TcpRecon tcpRecon = new TcpRecon();
                        divertDict.Add(c, tcpRecon);
                    }

                    divertDict[c].reassemble_tcp(tcpHeader.SequenceNumber, 0, buffer, (ulong) buffer.Length,
                        tcpHeader.Syn != 0,
                        Convert.ToUInt32(ipHeader.SourceAddress.GetAddressBytes()),
                        Convert.ToUInt32(ipHeader.DestinationAddress.GetAddressBytes()),
                        tcpHeader.SourcePort, tcpHeader.DestinationPort);
                }

                else if (address.Direction == DivertDirection.Outbound)
                {
                    diversion.CalculateChecksums(buffer, receiveLength, 0);
                    diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                    diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                }


                //asyncParseAndLog(receiveLength,file,diversion,buffer,ipHeader,tcpHeader);               
            }

            diversion.Close();
        }

        private async void asyncParseAndLog(uint receiveLength, FileStream file, Diversion diversion, byte[] buffer,
            IPHeader ipHeader, TCPHeader tcpHeader)
        {
            await Task.Run(() =>
            {
                diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                var start = ipHeader.HeaderLength + tcpHeader.HeaderLength;
                var count = receiveLength - start;
                Log("tcp header length : " + tcpHeader.HeaderLength + " \n ip header length : " + ipHeader.HeaderLength,file);
                Log($"ip : {ipHeader.SourceAddress} -> {ipHeader.DestinationAddress} \n port : {tcpHeader.SourcePort} -> {tcpHeader.DestinationPort}",file);
                Log(buffer, start, (int) count, file);
            });
        }

        private void Log(string o, FileStream file)
        {
            //Console.WriteLine(o);
            o = o + "\n";
            var bytes = Encoding.ASCII.GetBytes(o);
            file.Write(bytes, 0, bytes.Length);
        }

        private void Log(byte[] o, int start, int count, FileStream file)
        {
            //Console.WriteLine(o);                        
            file.Write(o, start, count);
        }
    }
}
