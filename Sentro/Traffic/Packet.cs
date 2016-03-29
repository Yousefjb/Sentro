using System;
using System.Text;
using System.Text.RegularExpressions;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    public class Packet
    {
        private const string Tag = "Packet";        
        private byte[] _packet;
        private uint _length;
        private FileLogger _fileLogger;       

        private int _tcpStart,_tcpHeaderLength;

        public Packet(byte[] rawPacket, uint packetLength)
        {
            _fileLogger = FileLogger.GetInstance();
            _packet = rawPacket;
            _length = packetLength;
            _tcpStart = (rawPacket[0] & 15)*4;
            _tcpHeaderLength = (rawPacket[_tcpStart + 12] >> 4)*4;
        }                                                    

        public byte[] RawPacket => _packet;
        public uint RawPacketLength => _length;                       

        public override int GetHashCode()
        {
            return ((SrcIp.GetHashCode() ^ SrcPort.GetHashCode()) as object).GetHashCode() ^
                   ((DestIp.GetHashCode() ^ DestPort.GetHashCode()) as object).GetHashCode();
        }

        public uint DestIp =>
            (uint)
                (((((((0 | _packet[16]) << 8) | _packet[17]) << 8) | _packet[18]) <<
                  8) | _packet[19]);

        public ushort SrcPort =>
            (ushort)
                (((0 | _packet[_tcpStart + 0]) << 8) | _packet[_tcpStart + 1]);

        public uint SrcIp =>
            (uint)
                (((((((0 | _packet[12]) << 8) | _packet[13]) << 8) | _packet[14]) <<
                  8) | _packet[15]);

        public ushort DestPort =>
            (ushort)
                (((0 | _packet[_tcpStart + 2]) << 8) | _packet[_tcpStart + 3]);
      
        public uint AckNumber =>
            (uint)
                (((((((0 | _packet[_tcpStart + 8]) << 8) | _packet[_tcpStart + 9]) << 8) | _packet[_tcpStart + 10]) <<
                  8) | _packet[_tcpStart + 11]);

        public uint SeqNumber =>
            (uint)
                (((((((0 | _packet[_tcpStart + 4]) << 8) | _packet[_tcpStart + 5]) << 8) | _packet[_tcpStart + 6]) <<
                  8) | _packet[_tcpStart + 7]);

        public ushort WindowSize =>
            (ushort)
                (((0 | _packet[_tcpStart + 14]) << 8) | _packet[_tcpStart + 15]);

        public ushort Id =>
            (ushort)
                (((0 | _packet[4]) << 8) | _packet[5]);

        public int TcpStart => _tcpStart;

        public byte WindowScale
        {
            get
            {
                var i = _tcpStart + 20;
                var j = _tcpStart + _tcpHeaderLength;
                while (i < j)                       
                {
                    if (_packet[i] == 1)
                        i++;
                    else if (_packet[i] == 2)
                        i += 4;
                    else if (_packet[i] == 4)
                        i += 2;
                    else if (_packet[i] == 3)
                        return _packet[i + 2];                    
                }
                return 0;
            }
        }

        public int DataStart => _tcpStart + _tcpHeaderLength;        
        public int DataLength => (int) _length - DataStart;

        public bool Fin => _packet[_tcpStart + 13] == 1 || _packet[_tcpStart + 13] == 9;
                
        public bool Syn => _packet[_tcpStart + 13] == 2 || _packet[_tcpStart + 13] == 10;     

        public bool Rst => _packet[_tcpStart + 13] == 4 || _packet[_tcpStart + 13] == 12;     

        public bool Ack => _packet[_tcpStart + 13] == 16 || _packet[_tcpStart + 13] == 24;
       
        public bool SynAck => _packet[_tcpStart + 13] == 18 || _packet[_tcpStart + 13] == 26;
               
        public bool FinAck => _packet[_tcpStart + 13] == 17 || _packet[_tcpStart + 13] == 25;
     

        private string uri = "";

        public bool IsHttpGet()
        {
            bool isHttpGet = false;

            var ascii = Encoding.ASCII.GetString(_packet, DataStart, (int) _length - DataStart);
            _fileLogger.Debug(Tag, ascii);
            var result = Regex.Match(ascii, CommonRegex.HttpGetUriMatch, RegexOptions.Multiline);
            if (result.Success)
            {
                var host = result.Groups["host"].Value.Trim();
                var path = result.Groups["path"].Value.Trim();
                uri = host + path;
                isHttpGet = true;
            }
            return isHttpGet;
        }

        public bool IsHttpResponse()
        {

            return HttpResponseHeaders != null;
        }

        private HttpResponseHeaders _httpResponseHeaders;

        public HttpResponseHeaders HttpResponseHeaders
        {            
            get
            {                
                if (_httpResponseHeaders != null)
                    return _httpResponseHeaders;

                _httpResponseHeaders = new HttpResponseHeaders();
                var ascii = Encoding.ASCII.GetString(_packet, DataStart, (int) _length - DataStart);
                var result = Regex.Match(ascii, CommonRegex.HttpContentLengthMatch,
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
                _httpResponseHeaders.ContentLength = result.Success ? Convert.ToInt32(result.Groups[1].Value) : 0;
                return _httpResponseHeaders;
            }
        }

        public string Uri
        {
            get
            {
                var requestUri = uri;
                if (requestUri.Length == 0)
                    requestUri = IsHttpGet() ? uri : "";
                return requestUri;
            }
        }      
    }
}
