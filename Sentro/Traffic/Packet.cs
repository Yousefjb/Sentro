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

        private int TcpStart,TcpHeaderLength;

        public Packet(byte[] rawPacket, uint packetLength)
        {
            _fileLogger = FileLogger.GetInstance();
            _packet = rawPacket;
            _length = packetLength;
            TcpStart = (rawPacket[0] & 15)*4;
            TcpHeaderLength = (rawPacket[TcpStart + 12] >> 4)*4;
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
                (((0 | _packet[TcpStart + 0]) << 8) | _packet[TcpStart + 1]);

        public uint SrcIp =>
            (uint)
                (((((((0 | _packet[12]) << 8) | _packet[13]) << 8) | _packet[14]) <<
                  8) | _packet[15]);

        public ushort DestPort =>
            (ushort)
                (((0 | _packet[TcpStart + 2]) << 8) | _packet[TcpStart + 3]);
      
        public uint AckNumber =>
            (uint)
                (((((((0 | _packet[TcpStart + 8]) << 8) | _packet[TcpStart + 9]) << 8) | _packet[TcpStart + 10]) <<
                  8) | _packet[TcpStart + 11]);

        public uint SeqNumber =>
            (uint)
                (((((((0 | _packet[TcpStart + 4]) << 8) | _packet[TcpStart + 5]) << 8) | _packet[TcpStart + 6]) <<
                  8) | _packet[TcpStart + 7]);

        public ushort WindowSize =>
            (ushort)
                (((0 | _packet[TcpStart + 14]) << 8) | _packet[TcpStart + 15]);

        public byte WindowScale
        {
            get
            {
                var i = TcpStart + 20;
                var j = TcpStart + TcpHeaderLength;
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

        public int DataStart => TcpStart + TcpHeaderLength;        
        public int DataLength => (int) _length - DataStart;

        public bool Fin
        {
            get
            {
                var fin = _packet[TcpStart + 13] | 1;
                return (fin == 1 || fin == 9);
            }
        }

        public bool Syn
        {
            get
            {
                var syn = _packet[TcpStart + 13] | 2;
                return (syn == 2 || syn == 10);
            }
        }    

        public bool Rst
        {
            get
            {
                var rst = _packet[TcpStart + 13] | 4;
                return (rst == 4 || rst == 12);
            }
        }

        public bool Ack
        {
            get
            {
                var ack = _packet[TcpStart + 13] | 16;
                return (ack == 16 || ack == 24);
            }
        }
            
        public bool SynAck => Syn && Ack;
        public bool FinAck => Fin && Ack;
        

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
