using PcapDotNet.Core;

namespace Sentro.Utilities
{
    /*
        Responsipility : define named methods for logging information
    */
    interface ILogger
    {
        void Log(string message);
        void Log(LivePacketDevice networkInterface);
    }
}
