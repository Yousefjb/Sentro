using System.Collections.Generic;
using Sentro.Traffic;

namespace Sentro.Utilities
{
    /*
        Responsibility : Shared Key Value Store for all classes
    */
    internal static class KvStore
    { 
        public static readonly Dictionary<string, string> IpMac;
        public static readonly Dictionary<int, Connection> Connections;
        public static readonly HashSet<string> TargetIps;

        static KvStore()
        {
            IpMac = new Dictionary<string, string>();
            TargetIps = new HashSet<string>();
            Connections = new Dictionary<int, Connection>();
        }
    }
}
