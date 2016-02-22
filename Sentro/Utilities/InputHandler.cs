using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PcapDotNet.Packets.IpV4;
using Sentro.ARP;
using Sentro.Traffic;

namespace Sentro.Utilities
{
    /*    
        Responsibility : Take input from user and convert it to command
        TODO : Reimplement this class
    */

    internal class InputHandler
    {
        public const string Tag = "InputHandler";

        public static void Status(string commands)
        {
            var arp = ArpSpoofer.GetInstance();
            Console.WriteLine("ARP Spoofer : {0}", arp.State());
            Console.WriteLine("Traffic Manager : not implemented");
            Console.WriteLine("Cache Manager : not implemented");
            Console.WriteLine("Analyzer : not implemented");
            Console.WriteLine("SSLstrip : not implemented");
        }

        /**
        usage samples 

        arp 192.168.1.11 spoof 192.168.1.1 192.168.1.7 192.168.1.9
        arp 192.168.1.11 spoof all
        arp 192.168.1.11 spoof all -192.168.1.1 -192.168.1.7
        arp +192.168.1.1
        arp -192.168.1.1
        arp +192.168.1.1 -192.168.1.7
        arp pause
        arp resume
        arp stop
        */

        public static void Arp(string command)
        {
            //TODO: replace logging with real functions            

            #region arp command regex expressions

            /*start with arp (then space then + or - followed by an ip) at least once */
            const string arpIncludeExclude =
                @"^arp(?: (?:\+|\-)" + CommonRegex.Ip + @")+$";

            /*start with arp then space then ip then space then spoof (then space then ip) at least once*/
            const string arpSpoofSet =
                @"^arp " + CommonRegex.Ip + @" spoof(?: " + CommonRegex.Ip + @")+$";

            /*start with arp then space then ip then space then spoof then space then all (then space then - then ip) any numberof times */
            const string arpSpoofAll =
                @"^arp " + CommonRegex.Ip + @" spoof all(?: (?:\-)" + CommonRegex.Ip + @")*$";

            /*start with arp then end of line*/
            const string arpUsage = @"^arp$";

            /*start with arp then space then pause then end of line*/
            const string arpPuase = @"^arp pause$";

            /*start with arp then space then resume then end of line*/
            const string arpResume = @"^arp resume$";

            /*start with arp then space then start then end of line*/
            const string arpStart = @"^arp start$";

            /*start with arp then space then stop then end of line*/
            const string arpStop = @"^arp stop$";

            #endregion

            ILogger logger = ConsoleLogger.GetInstance();
            IArpSpoofer spoofer = ArpSpoofer.GetInstance();

            #region expression evaluation against command 

            if (Regex.IsMatch(command, arpUsage))
                spoofer.Usage();

            else if (Regex.IsMatch(command, arpPuase))
                spoofer.Pause();

            else if (Regex.IsMatch(command, arpResume))
                spoofer.Resume();

            else if (Regex.IsMatch(command, arpStart))
                spoofer.Start();

            else if (Regex.IsMatch(command, arpStop))
                spoofer.Stop();

            else if (Regex.IsMatch(command, arpIncludeExclude))
            {
                var included = Regex.Matches(command, @"(?:\+)" + CommonRegex.Ip);
                var excluded = Regex.Matches(command, @"(?:\-)" + CommonRegex.Ip);
                foreach (Match ip in included)
                {
                    //spoofer.Include(new IpV4Address(ip.Value));
                }

                foreach (Match ip in excluded)
                {
                    //spoofer.Exclude(new IpV4Address(ip.Value));
                }
            }

            else if (Regex.IsMatch(command, arpSpoofSet))
            {
                var matches = Regex.Matches(command, CommonRegex.Ip);
                var ips = (from Match ip in matches select ip.Value).ToList();
                var myip = ips[0];
                ips.RemoveAt(0);
                spoofer.Spoof(myip, new HashSet<string>(ips));
            }

            else if (Regex.IsMatch(command, arpSpoofAll))
            {
                var matches = Regex.Matches(command, CommonRegex.Ip);
                var ips = (from Match ip in matches select new IpV4Address(ip.Value)).ToList();
                var myip = ips[0];
                ips.RemoveAt(0);
                //spoofer.Spoof(myip);
                //spoofer.Exclude(ips);
            }

            #endregion
        }

        public static void Traffic(string command)
        {
            if (command.Contains("start")) { 
                 TrafficManager.GetInstance().Start();                
            }
            else if (command.Contains("stop"))
                TrafficManager.GetInstance().Stop();
        }

    }
}
