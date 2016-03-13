using System.Collections.Generic;

namespace Sentro.Utilities
{
    /*
        Responsibility : Shared Key Value Store for all classes
    */
    internal static class KvStore
    { 
        public static readonly Dictionary<string, string> IpMac;
        public static readonly HashSet<string> TargetIps;               

        static KvStore()
        {
            IpMac = new Dictionary<string, string>();
            TargetIps = new HashSet<string>();
        }
    }
}
