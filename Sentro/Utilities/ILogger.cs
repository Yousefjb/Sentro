using PcapDotNet.Core;

namespace Sentro.Utilities
{
    /*
        Responsipility : define named methods for logging information
    */
    interface ILogger
    {        
        void Debug(string tag, string message);
        void Info(string tag, string message);
        void Error(string tag, string message);
        void Log(LivePacketDevice networkInterface);
    }
}
