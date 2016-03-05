using System;
using System.Collections.Generic;
using System.IO;

namespace Sentro.Utilities
{
    /*
        Responsibility : Log info to a file in the logging folder
    */
    internal class FileLogger : ILogger, IDisposable
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
            string path = fileHierarchy.LogsDirectory + "\\" + DateTime.Today.Ticks;
            var directoryInfo = new FileInfo(path).Directory;
            directoryInfo?.Create();
            if (directoryInfo != null) directoryInfo.Attributes &= ~FileAttributes.ReadOnly;
            _file = new StreamWriter(path, true) {AutoFlush = true};
        }

        public void Debug(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Debug {tag} {message}", _file);
            _file.Flush();
        }

        public void Info(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Info {tag} {message}",_file);
            _file.Flush();
        }

        public void Error(string tag, string message)
        {
            var time = $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}";
            Writer.Write($"{time} Error {tag} {message}",_file);
            _file.Flush();
        }

        public void Dispose()
        {
            _file.Close();
        }
    }
}
