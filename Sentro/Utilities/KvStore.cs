using System.Collections.Concurrent;
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
        public static readonly ConcurrentDictionary<int, Connection> Connections;
        public static readonly ConcurrentDictionary<int, ConnectionController> ConnectionControllers;
        public static readonly HashSet<string> TargetIps;

        static KvStore()
        {
            IpMac = new Dictionary<string, string>();
            TargetIps = new HashSet<string>();
            Connections = new ConcurrentDictionary<int, Connection>();
            ConnectionControllers = new ConcurrentDictionary<int, ConnectionController>();
        }
    }
}
