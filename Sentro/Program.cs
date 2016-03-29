using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PcapDotNet.Base;
using Sentro.ARP;
using Sentro.Traffic;
using Sentro.Utilities;

namespace Sentro
{
    public class Program
    {
        public const string Tag = "Program";

        private static void Main(string[] args)
        {
            Console.Title = "Sentro";
            Console.WriteLine(Sento);
            using (new SingleGlobalInstance(1000))
            {
                _handler = ConsoleEventCallback; //used to detect terminiation
                SetConsoleCtrlHandler(_handler, true); //using mutix and callbacks
                Console.CancelKeyPress += delegate { CleanUpSentro(); };                
                string command;
                do
                {                    
                    command = ReadLineAsync().Result;
                    if (command.IsNullOrEmpty())
                        continue;

                    var function = command.Split(' ')[0].ToLower();
                    switch (function)
                    {
                        case "arp":
                            new Thread(() => InputHandler.Arp(command)).Start();
                            break;
                        case "traffic":
                            InputHandler.Traffic(command);
                            break;                       
                        case "exit":
                            break;
                    }

                } while (command != null && !command.Equals("exit"));

                CleanUpSentro();
            }

        }

        #region Clean up before Sentro termination
        static bool ConsoleEventCallback(int eventType)
        {
            CleanUpSentro();
            return false;
        }

        static ConsoleEventDelegate _handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        
        #endregion

        private async static void CleanUpSentro()
        {            
            var arp = ArpSpoofer.GetInstance();
            var state = arp.State();

            if (state == ArpSpoofer.Status.Started ||
                state == ArpSpoofer.Status.Starting ||
                state == ArpSpoofer.Status.Paused)
                await Task.Run(()=>ArpSpoofer.GetInstance().Stop());

            TrafficManager.GetInstance().Stop();
        }

        private static async Task<string> ReadLineAsync()
        {
            return await Task.Run(() => Console.ReadLine());
        }

   
        private const string Sento = @"
███████╗███████╗███╗   ██╗████████╗██████╗  ██████╗ 
██╔════╝██╔════╝████╗  ██║╚══██╔══╝██╔══██╗██╔═══██╗
███████╗█████╗  ██╔██╗ ██║   ██║   ██████╔╝██║   ██║
╚════██║██╔══╝  ██║╚██╗██║   ██║   ██╔══██╗██║   ██║
███████║███████╗██║ ╚████║   ██║   ██║  ██║╚██████╔╝
╚══════╝╚══════╝╚═╝  ╚═══╝   ╚═╝   ╚═╝  ╚═╝ ╚═════╝ 
Transparent Cache Server For Small and Medium Networks                                                    
";
    }
}
