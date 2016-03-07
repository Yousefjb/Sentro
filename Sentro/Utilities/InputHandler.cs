using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sentro.ARP;
using Sentro.Traffic;

namespace Sentro.Utilities
{
    /*    
        Responsibility : Take input from user and convert it to command        
    */
    internal class InputHandler
    {
        public const string Tag = "InputHandler";
       
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
            #region arp command regex
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
            
            IArpSpoofer spoofer = ArpSpoofer.GetInstance();

            #region expression evaluation against command 

            if (Regex.IsMatch(command, arpUsage))
                Console.WriteLine(spoofer.Usage());

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
                var hashset = new HashSet<string>();

                foreach (Match ip in included)                
                    hashset.Add(ip.Value);
                
                spoofer.Include(hashset);
                
                hashset.Clear();

                foreach (Match ip in excluded)
                    hashset.Add(ip.Value);

                spoofer.Exclude(hashset);
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
                var ips = (from Match ip in matches select ip.Value).ToList();
                var myip = ips[0];
                ips.RemoveAt(0);
                spoofer.Spoof(myip);
                spoofer.Exclude(new HashSet<string>(ips));
            }

            #endregion
        }

        public static void Traffic(string command)
        {
            #region traffic command regex
            const string trafficStart =@"^traffic start$";
            const string trafficStop = @"^traffic stop$";
            #endregion

            #region expression evaluation against command 
            if (Regex.IsMatch(command, trafficStart))
                TrafficManager.GetInstance().Start();

            else if (Regex.IsMatch(command,trafficStop))
                TrafficManager.GetInstance().Stop();
            #endregion
        }

    }
}
