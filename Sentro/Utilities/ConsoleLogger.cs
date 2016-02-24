using System;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    /*
        Responsipility : Log information on console screen
    */
    class ConsoleLogger : ILogger
    {
        public const string Tag = "ConsoleLogger";
        private static ConsoleLogger _consoleLogger;

        public static ConsoleLogger GetInstance()
        {
            return _consoleLogger ?? (_consoleLogger = new ConsoleLogger());
        }

        private ConsoleLogger()
        {            
        }

        public void Debug(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Console.WriteLine("\n{0} Debug {1} {2}\n", time, tag, message);
        }

        public void Info(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Console.WriteLine("\n{0} Info {1} {2}\n", time, tag, message);
        }

        public void Error(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Console.WriteLine("\n{0} Error {1} {2}\n", time, tag, message);
        }

        public void Log(LivePacketDevice networkInterface)
        {
            Console.WriteLine($"{networkInterface.Name ?? "Name not found"}");
            Console.WriteLine($"\t description : {networkInterface.Description ?? "Description not found"}");
            Console.WriteLine((networkInterface.Attributes & DeviceAttributes.Loopback) == DeviceAttributes.Loopback
                ? "\t is loopback "
                : "\t is not loopback");

            foreach (var deviceAddress in networkInterface.Addresses)
            {
                Console.WriteLine($"\t family : {deviceAddress.Address.Family}");
                Console.WriteLine(deviceAddress.Address != null
                    ? $"\t address : {deviceAddress.Address}"
                    : "\t address not found");
                Console.WriteLine(deviceAddress.Netmask != null
                    ? $"\t netmask : {deviceAddress.Netmask}"
                    : "\t netmask not found");
                Console.WriteLine(deviceAddress.Broadcast != null
                    ? $"\t brodcast : {deviceAddress.Broadcast}"
                    : "\t brodcast not found");
                Console.WriteLine(deviceAddress.Destination != null
                    ? $"\t destination : {deviceAddress.Destination}"
                    : "\t destination not found");
            }

            Console.WriteLine();
        }
    }
}
