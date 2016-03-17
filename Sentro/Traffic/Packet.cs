using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    public class Packet
    {
        private const string Tag = "Packet";
        private TCPHeader _tcpHeader;
        private IPHeader _ipHeader;
        private byte[] _packet;
        private uint _length;
        private FileLogger _fileLogger;

        public Packet(byte[] rawPacket, uint packetLength, TCPHeader parsedTcpHeader, IPHeader parsedIpHeader)
        {
            _tcpHeader = parsedTcpHeader;
            _ipHeader = parsedIpHeader;
            _packet = rawPacket;
            _length = packetLength;            
        }

        private int TcpStart,TcpHeaderLength;

        public Packet(byte[] rawPacket, uint packetLength)
        {
            _fileLogger = FileLogger.GetInstance();
            _packet = rawPacket;
            _length = packetLength;
            TcpStart = (rawPacket[0] & 15)*4;
            TcpHeaderLength = (rawPacket[TcpStart + 12] >> 4)*4;
        }

        public IPAddress SourceIp
        {
            get { return _ipHeader.SourceAddress; }
            set { _ipHeader.SourceAddress = value; }
        }

        public IPAddress DestinationIp
        {
            get { return _ipHeader.DestinationAddress; }
            set { _ipHeader.DestinationAddress = value; }
        }

        public ushort SourcePort
        {
            get { return _tcpHeader.SourcePort; }
            set { _tcpHeader.SourcePort = value; }
        }
        
        public ushort DestinationPort
        {
            get { return _tcpHeader.DestinationPort; }
            set { _tcpHeader.DestinationPort = value; }
        }

        public int DataLength => (int) _length - Offset;
  
        private int Offset => _tcpHeader.HeaderLength*4 + _ipHeader.HeaderLength*4;
        public byte[] Data => _packet.Skip(Offset).Take(DataLength).ToArray();             

        public ushort Psh => _tcpHeader.Psh;

        public byte[] RawPacket => _packet;
        public uint RawPacketLength => _length;       

        public TCPHeader TcpHeader => _tcpHeader;
        public IPHeader IpHeader => _ipHeader;

        public override int GetHashCode()
        {
            return ((SrcIp.GetHashCode() ^ SrcPort.GetHashCode()) as object).GetHashCode() ^
                   ((DestIp.GetHashCode() ^ DestPort.GetHashCode()) as object).GetHashCode();
        }
        
        public uint DestIp => BitConverter.ToUInt32(new[]
        {
            _packet[19],
            _packet[18],
            _packet[17],
            _packet[16]
        }, 0);

        public ushort SrcPort => BitConverter.ToUInt16(new[]
        {
            _packet[TcpStart + 1],
            _packet[TcpStart + 0]
        }, 0);

        public uint SrcIp => BitConverter.ToUInt32(new[]
        {
            _packet[15],
            _packet[14],
            _packet[13],
            _packet[12]
        }, 0);

        public ushort DestPort => BitConverter.ToUInt16(new[]
        {
            _packet[TcpStart + 3],
            _packet[TcpStart + 2]
        }, 0);

        public uint AckNumber => BitConverter.ToUInt32(new[]
        {
            _packet[TcpStart + 11],
            _packet[TcpStart + 10],
            _packet[TcpStart + 9],
            _packet[TcpStart + 8]
        }, 0);

        public uint SeqNumber => BitConverter.ToUInt32(new[]
        {
            _packet[TcpStart + 7],
            _packet[TcpStart + 6],
            _packet[TcpStart + 5],
            _packet[TcpStart + 4]
        }, 0);

        public ushort WindowSize => BitConverter.ToUInt16(new[]
        {
            _packet[TcpStart + 15],
            _packet[TcpStart + 14]
        }, 0);

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
        public int DataLengthV2 => (int) _length - DataStart;

        public bool Fin => (_packet[TcpStart + 13] & 1) == 1;
        public bool Syn => (_packet[TcpStart + 13] & 2) == 2;
        public bool Rst => (_packet[TcpStart + 13] & 4) == 4;
        public bool Ack => (_packet[TcpStart + 13] & 16) == 16;
        public bool SynAck => Syn && Ack;
        public bool FinAck => Fin && Ack;
        


        private string uri = "";

        public bool IsHttpGet()
        {            
            var ascii = Encoding.ASCII.GetString(_packet, DataStart, (int) _length - DataStart);            
            _fileLogger.Debug(Tag,ascii);
            var result = Regex.Match(ascii, CommonRegex.HttpGetUriMatch, RegexOptions.Multiline);            
            if (result.Success)
            {
                var host = result.Groups["host"].Value.Trim();
                var path = result.Groups["path"].Value.Trim();
                uri = host + path;                          
                return true;
            }
            return false;
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
