using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Sentro.Utilities
{
    /*
        Responsibility : Log info to a file in the logging folder
    */
    internal class FileLogger : ILogger, IDisposable
    {
        public const string Tag = "FileLogger";
        private readonly FileStream _file;
        private static FileLogger _fileLogger;
        private static uint index;        

        public static FileLogger GetInstance()
        {
            return _fileLogger ?? (_fileLogger = new FileLogger());            
        }

        private FileLogger()
        {
            var fileHierarchy = FileHierarchy.GetInstance();
            string path = fileHierarchy.LogsDirectory + "\\" + DateTime.Today.Ticks;
            var directoryInfo = new FileInfo(path).Directory;
            directoryInfo?.Create();
            if (directoryInfo != null) directoryInfo.Attributes &= ~FileAttributes.ReadOnly;
            _file = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        }        
                
        public async void Debug(string tag, string message)
        {
            var time = $"{index++} {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            var bytes = Encoding.ASCII.GetBytes($"{time} Debug {tag} {message}\n");
            await _file.WriteAsync(bytes, 0, bytes.Length);
            await _file.FlushAsync();
        }

        public async void Info(string tag, string message)
        {
            var time = $"{index++} {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            var bytes = Encoding.ASCII.GetBytes($"{time} Info {tag} {message}\n");
            await _file.WriteAsync(bytes, 0, bytes.Length);
            await _file.FlushAsync();
        }

        public async void Error(string tag, string message)
        {
            var time = $"{index++} {DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            var bytes = Encoding.ASCII.GetBytes($"{time} Error {tag} {message}\n");
            await _file.WriteAsync(bytes, 0, bytes.Length);
            await _file.FlushAsync();
        }

        public void Dispose()
        {            
            _file.Close();
        }
    }   
}
