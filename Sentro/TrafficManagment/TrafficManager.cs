using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

       
        private void Divert(bool forwardMode)
        {
            var divertDict = new Dictionary<Connection, TcpBuffer>();
            Diversion diversion;

            string filter = "tcp.DstPort == 80 or tcp.SrcPort == 80";
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

            logger.Info(Tag,"started traffic manager");
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

                //Connection connection = new Connection(ipHeader.SourceAddress.ToString(), tcpHeader.SourcePort,
                //        ipHeader.DestinationAddress.ToString(), tcpHeader.DestinationPort);

                //if (divertDict.ContainsKey(connection))
                //{
                //    /*pass*/                    
                //    diversion.SendAsync(buffer, receiveLength, address, ref sendLength);

                //    if (address.Direction == DivertDirection.Outbound)
                //        throw new NotImplementedException("interleved requests");
                //    else
                //    {
                //        diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                //        if (tcpHeader.Psh == 1 /*&& parseToHTTP*/)
                //        {
                //            /*pass to cache manager*/
                //        }
                //        else
                //        {
                //            /*put in buffer*/

                //            bool isBufferFull = true;/*check here*/
                //            if (isBufferFull)
                //            {
                //                /*flush buffer*/
                //            }
                //        }

                //    }
                //}
                //else
                //{
                //    if (address.Direction == DivertDirection.Inbound)
                //    {
                //        /*pass*/
                //        diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                //    }
                //    else
                //    {
                //        if (tcpHeader.Psh == 0 /*|| CantParseToHTTP*/)
                //        {
                //            diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                //        }
                //        else
                //        {
                //            /*ask cache manager for cache*/
                //            bool isCached = false;
                //            if (isCached)
                //            {
                //                /*send response*/
                //                /*reset connection*/                                
                //            }
                //            else
                //            {
                //                divertDict.Add(connection,new TcpRecon());
                //                diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                //            }
                //        }
                //    }
                //}                
               

                if (address.Direction == DivertDirection.Outbound)
                {
                    diversion.CalculateChecksums(buffer, receiveLength, 0);
                }
                diversion.SendAsync(buffer, receiveLength, address, ref sendLength);
                asyncParseAndLog(receiveLength,file,diversion,buffer,ipHeader,tcpHeader);               
            }


            diversion.Close();
        }

        private async void asyncParseAndLog(uint receiveLength, FileStream file, Diversion diversion, byte[] buffer,
            IPHeader ipHeader, TCPHeader tcpHeader)
        {
            await Task.Run(() =>
            {
                diversion.ParsePacket(buffer, receiveLength, ipHeader, null, null, null, tcpHeader, null);
                var start = ipHeader.HeaderLength*4 + tcpHeader.HeaderLength*4;                                           
                var count = receiveLength - start;

                if (count <= 0)
                    return;

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

        public void Stop()
        {
            running = false;
        }


        private bool isHttpGet(ref byte[] packetBytes, int offset)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset, packetBytes.Length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpGet);
        }

        private bool isHttpResponse(ref byte[] packetBytes, int offset)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset, packetBytes.Length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpResonse);
        }

    }
}
