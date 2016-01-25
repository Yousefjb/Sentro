using System;
using PcapDotNet.Core;

namespace Sentro.Utilities
{
    class FileLogger : ILogger
    {
        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        public void Log(LivePacketDevice networkInterface)
        {
            throw new NotImplementedException();
        }
    }
}
