using System.Text;
using System.Text.RegularExpressions;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{    
    /*
        Responsibility : a group of functions for all other classes
        TODO: remove this class
    */
    static class HelperFunctions
    {
        public static int Offset(TCPHeader tcpHeader, IPHeader ipHeader)
        {
            if (tcpHeader == null || ipHeader == null)
                return 0;
            return tcpHeader.HeaderLength*4 + ipHeader.HeaderLength*4;
        }   
    }
}
