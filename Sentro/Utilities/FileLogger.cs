using System;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    class FileLogger : ILogger
    {
        public const string Tag = "FileLogger";        
        public void Debug(string tag, string message)
        {
            throw new NotImplementedException();
        }

        public void Info(string tag, string message)
        {
            throw new NotImplementedException();
        }

        public void Error(string tag, string message)
        {
            throw new NotImplementedException();
        }

        public void Log(LivePacketDevice networkInterface)
        {
            throw new NotImplementedException();
        }
    }
}
