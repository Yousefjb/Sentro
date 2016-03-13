using System;
using System.IO;

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
            _file = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        }

        public void Debug(string tag, string message)
        {            
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Debug {tag} {message}\n", _file);
            _file.Flush();
        }

        public void Info(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Info {tag} {message}\n",_file);
            _file.Flush();
        }

        public void Error(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Error {tag} {message}\n",_file);
            _file.Flush();
        }

        public void Dispose()
        {
            _file.Close();
        }
    }
}
