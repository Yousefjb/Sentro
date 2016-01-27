using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PcapDotNet.Base;
using Sentro.ARPSpoofer;
using Sentro.Utilities;

namespace Sentro
{
    /*
        Responsipility : Entry point of the application
    */
    public class Program
    {
        public const string Tag = "Program";
        private static void Main(string[] args)
        {
            ILogger logger = ConsoleLogger.GetInstance();
            Console.Title = "Sentro";
            Console.WriteLine(Sento);
            MainPoint:
            try
            {
                
                using (new SingleGlobalInstance(1000))
                {
                    _handler = ConsoleEventCallback;        //used to detect terminiation
                    SetConsoleCtrlHandler(_handler, true);  //using mutix and callbacks
     
                    string readLine;
                    do
                    {                       
                        readLine = ReadLineAsync().Result;
                        if(readLine.IsNullOrEmpty())
                            continue;

                        var commands = readLine.Split(' ');
                        switch (commands[0].ToLower())
                        {
                            case "status":
                                Task.Run(() => InputHandler.Status(readLine));
                                break;
                            case "arp":
                                Task.Run(() => InputHandler.Arp(readLine));
                                break;                            
                            default:
                                logger.Log(Tag,LogLevel.Info,$"{commands[0]} undefined");
                                break;                                
                        }

                    } while (!readLine.Equals("exit"));
                }
            }
            catch (Exception e)
            {
                //TODO: write try catch in each class insted of catching all here
                logger.Log(Tag,LogLevel.Error,e.Message);
                logger.Log(Tag,LogLevel.StackTrace,e.StackTrace);
                goto MainPoint;
            }
         }

        #region Clean up before Sentro termination
        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                var arp = ArpSpoofer.GetInstance();
                var state = arp.State();

                if (state == ArpSpoofer.Status.Started ||
                    state == ArpSpoofer.Status.Starting ||
                    state == ArpSpoofer.Status.Paused)
                    ArpSpoofer.GetInstance().Stop();

            }
            return false;
        }

        static ConsoleEventDelegate _handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        
        #endregion

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
