using System.Net;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    class ForwardDiverser : TrafficManager
    {
        public const string DefaultFilter = "tcp.DstPort == 80 or tcp.SrcPort == 80";
        public static bool IsIncomePacket(Address address, IPAddress destinationIp)
        {
            return KvStore.TargetIps.Contains(destinationIp.ToString());
        }
    }
}
