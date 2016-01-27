using System;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    class FileLogger : ILogger
    {
        public const string Tag = "FileLogger";
        public void Log(string tag, string level, string message)
        {
            throw new NotImplementedException();
        }

        public void Log(LivePacketDevice networkInterface)
        {
            throw new NotImplementedException();
        }
    }
}
