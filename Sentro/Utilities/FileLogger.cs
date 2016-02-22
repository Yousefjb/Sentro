using System;
using System.IO;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    class FileLogger : ILogger,IDisposable
    {
        public const string Tag = "FileLogger";
        private readonly StreamWriter _file;
        private static FileLogger _fileLogger;

        public static FileLogger GetInstance()
        {
            return _fileLogger ?? (_fileLogger = new FileLogger());
        }

        private FileLogger()
        {
            var fileHierarchy = FileHierarchy.GetInstance();            
            _file = new StreamWriter(fileHierarchy.LogsDirectory+"/"+DateTime.UtcNow,true);
            _file.AutoFlush = true;
        }
       

        public void Debug(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            _file.WriteLine($"{time} Debug {tag} {message}");            
        }

        public void Info(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            _file.WriteLine($"{time} Info {tag} {message}");            
        }

        public void Error(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            _file.WriteLine($"{time} Error {tag} {message}");            
        }               

        public void Dispose()
        {
            _file.Close();
        }
    }
}
