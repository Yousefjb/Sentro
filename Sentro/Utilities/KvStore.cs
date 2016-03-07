using System.Collections.Generic;

namespace Sentro.Utilities
{
    /*
        Responsibility : Shared Key Value Store for all classes
    */
    internal class KvStore
    {
        private static KvStore _kvStore;
        public Dictionary<string, string> IpMac;
        public HashSet<string> TargetIps;        
        public static KvStore GetInstance()
        {
            return _kvStore ?? (_kvStore = new KvStore());
        }

        private KvStore()
        {
            IpMac = new Dictionary<string, string>();
            TargetIps = new HashSet<string>();            
        }

        
    }
}
