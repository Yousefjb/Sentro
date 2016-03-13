using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : TcpStream that hold request bytes with http request specific functions
    */
    internal class SentroRequest
    {
        private const string Tag = "SentroRequest";   
        private string _requestUri = "";                
        private readonly FileLogger _fileLogger;        
        private readonly Packet _packet;
      
        public SentroRequest(Packet packet)
        {
            _packet = packet;
            _fileLogger = FileLogger.GetInstance();
        }
        
        private static string _ascii = "";
        private bool ContainHttpGet()
        {
            _ascii = Encoding.ASCII.GetString(_packet.Data);
            return Regex.IsMatch(_ascii, CommonRegex.HttpGet);
        }
        public string RequestUri()
        {
            try
            {
                if (_requestUri.Length != 0)
                    return _requestUri;
                
                var ascii = _ascii.Length == 0 ? Encoding.ASCII.GetString(_packet.Data) : _ascii;
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

        public bool IsValid => _packet.Psh == 1 && _packet.DataLength > 0 && ContainHttpGet();

        public string RequestUriHashed()
        {                        
            var normalized = Normalizer.GetInstance().Normalize(RequestUri());
            return Murmur2.HashX8(normalized,Encoding.ASCII);
        }

        public IPAddress DestinationIp => _packet.DestinationIp;

        public ushort DestinationPort => _packet.DestinationPort;

        public IPAddress SourceIp => _packet.SourceIp;

        public ushort SourcePort => _packet.SourcePort;

        public Packet Packet => _packet;
    }
}
