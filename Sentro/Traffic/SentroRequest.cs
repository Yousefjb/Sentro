using System;
using System.Text;
using System.Text.RegularExpressions;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : TcpStream that hold request bytes with http request specific functions
    */
    internal class SentroRequest
    {
        public const string Tag = "SentroRequest";   
        private string _requestUri = "";
        public byte[] _packetBytes;
        private const int MTU = 1500;
        private FileLogger _fileLogger;
        private TCPHeader _tcpHeader;
        private IPHeader _ipHeader;

        public SentroRequest(byte[] packetBytes, int length)
        {
            _fileLogger = FileLogger.GetInstance();
            if (length >= MTU)
            {
                _fileLogger.Error(Tag,"Packet size exceeded MTU");
                return;
            }
            _packetBytes = new byte[length];
            Array.Copy(packetBytes,_packetBytes,length);
            TrafficManager.GetInstance().Parse(_packetBytes, (uint)_packetBytes.Length, out _tcpHeader, out _ipHeader);
        }

        public static SentroRequest CreateFromBytes(byte[] bytes, int length)
        {
            var request = new SentroRequest(bytes,length);            
            return request;
        }

        public string RequestUri()
        {
            try
            {
                if (_requestUri.Length != 0)
                    return _requestUri;

                int offset = Offset();
                var ascii = Encoding.ASCII.GetString(_packetBytes, offset, _packetBytes.Length - offset);
                var result = Regex.Match(ascii, CommonRegex.HttpGetUriMatch, RegexOptions.Multiline);
                string path = result.Groups[1].Value;
                string host = result.Groups[2].Value.Replace("\r", "");
                _requestUri = host + path;
                return _requestUri;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag, e.ToString());
                return "";
            }
        }

        private int Offset()
        {                            
            return HelperFunctions.Offset(_tcpHeader, _ipHeader);
        }        

        public string RequestUriHashed()
        {                        
            var normalized = Normalizer.GetInstance().Normalize(RequestUri());
            return Murmur2.HashX8(normalized,Encoding.ASCII);
        }

        public byte[] DestinationIp()
        {
            return _ipHeader.DestinationAddress.GetAddressBytes();
        }

        public byte[] DestinationPort()
        {
            return BitConverter.GetBytes(_tcpHeader.DestinationPort);
        }

        public byte[] SourceIp()
        {
            return _ipHeader.SourceAddress.GetAddressBytes();
        }

        public byte[] SourcePort()
        {
            return BitConverter.GetBytes(_tcpHeader.SourcePort);
        }
    }
}
