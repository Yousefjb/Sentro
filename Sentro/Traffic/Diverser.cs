using System;
using System.Net;
using Divert.Net;

namespace Sentro.Traffic
{
    internal class Diverser : TrafficManager
    {        
        public const string DefaultFilter = "tcp.DstPort == 80 or tcp.SrcPort == 80";

        public static bool IsIncomePacket(Address address, IPAddress destinationIp)
        {            
            return address.Direction == DivertDirection.Inbound;
        }

        public Diverser(string filter = "true")
        {    

        }



        public void SetFilter(string filter)
        {

        }

        public void Start()
        {

        }

        public void Stop()
        {

        }
    }
}
