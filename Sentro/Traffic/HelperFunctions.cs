using System.Text;
using System.Text.RegularExpressions;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    //TODO : why ip header should be multiplied by 4 ?
    // Tcp is multiplied due to it being in words but why ip header ?
    static class HelperFunctions
    {
        public static int Offset(TCPHeader tcpHeader, IPHeader ipHeader)
        {
            if (tcpHeader == null || ipHeader == null)
                return 0;
            return tcpHeader.HeaderLength*4 + ipHeader.HeaderLength*4;
        }

        public static bool IsHttpGet(byte[] packetBytes, int offset, uint length)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset, (int)length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpGet);
        }

        public static bool IsHttpResponse(byte[] packetBytes, int offset, uint length)
        {
            string http = Encoding.ASCII.GetString(packetBytes, offset, (int)length - offset);
            return Regex.IsMatch(http, CommonRegex.HttpResonse);
        }
    }
}
