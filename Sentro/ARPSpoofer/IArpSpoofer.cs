using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using PcapDotNet.Packets.IpV4;

namespace Sentro.ARPSpoofer
{
    interface IArpSpoofer
    {
        void Spoof(string myIp, HashSet<string> targets);
        void Spoof(string myIp);
        void Include(string target);
        void Include(HashSet<string> targets);
        void Exclude(string target);
        void Exclude(HashSet<string> targets);
        void Stop();
        void Pause();
        void Resume();
        void Start();
        void Usage();        

    }
}
