using System;
using System.IO;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    class FileLogger : ILogger,IDisposable
    {
        public const string Tag = "FileLogger";
        private readonly StreamWriter _file;

        public FileLogger(string path, string name)
        {
            _file = new StreamWriter(path+ "\\" + name);            
        }

        public void Debug(string tag, string message)
        {  
            _file.WriteLine($"Debug {tag} {message}");
            _file.Flush();
        }

        public void Info(string tag, string message)
        {
            _file.WriteLine($"Info {tag} {message}");
            _file.Flush();
        }

        public void Error(string tag, string message)
        {
            _file.WriteLine($"Error {tag} {message}");
            _file.Flush();
        }        

        public void Log(LivePacketDevice networkInterface)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _file.Close();
        }
    }
}
