﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using Divert.Net;
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
        private bool _running;               

        public TrafficManager()
        {                    
            _fileLogger = FileLogger.GetInstance();            
        }

        public static TrafficManager GetInstance()
        {
            return _trafficManager ?? (_trafficManager = new TrafficManager());
        }


        private void Divert(DivertLayer layer)
        {
            const string filter = "tcp.DstPort == 80 or tcp.SrcPort == 80 or tcp.DstPort == 8082 or tcp.SrcPort == 8082";
            Diversion diversion;

            try
            {
                diversion = Diversion.Open(filter, layer,-1000, 0);
                diversion.SetParam(DivertParam.QueueLength, 8192);
                diversion.SetParam(DivertParam.QueueTime, 2048);
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

            

            while (_running)
            {
                var buffer = new byte[2048];
                var address = new Address();
                uint receiveLength = 0;                
                if (!diversion.Receive(buffer, address, ref receiveLength))
                {
                    _fileLogger.Error(Tag, $"Failed to receive packet with error {Marshal.GetLastWin32Error()}");
                    continue;
                }

                var packet = new Packet(buffer, receiveLength);

                var hash = packet.GetHashCode();
                if (!KvStore.Connections.ContainsKey(hash))
                    KvStore.Connections.TryAdd(hash, new Connection(diversion,address) {HashCode = hash});

                //if (!KvStore.ConnectionControllers.ContainsKey(hash))
                //    KvStore.ConnectionControllers.TryAdd(hash, new ConnectionController(packet,diversion));

                //Controlling Logic maybe              
                KvStore.Connections[hash].Add(packet,address);
                //KvStore.ConnectionControllers[hash].Push(packet);
                //Monitoring Logic maybe
            }
        }

        public void Stop()
        {
            try
            {
                foreach (var connection in KvStore.Connections.Values)
                {
                    connection.ClearResources();
                }
                _running = false;
            }
            catch (Exception e)
            {                                
            }                 
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            new Thread(CallDivertNetworkForward).Start();
            new Thread(CallDivertNetwork).Start();

        }

        private void CallDivertNetworkForward()
        {
            Divert(DivertLayer.NetworkForward);
        }

        private void CallDivertNetwork()
        {
            Divert(DivertLayer.Network);
        }
    }
}
