using System;
using System.Linq;
using System.Net;
using Divert.Net;

namespace Sentro.Traffic
{
    public class Packet
    {
        private TCPHeader _tcpHeader;
        private IPHeader _ipHeader;
        private byte[] _packet;
        private uint _length;

        public Packet(byte[] rawPacket, uint packetLength, TCPHeader parsedTcpHeader, IPHeader parsedIpHeader)
        {
            _tcpHeader = parsedTcpHeader;
            _ipHeader = parsedIpHeader;
            _packet = rawPacket;
            _length = packetLength;
        }

        private int TcpStart;
        public Packet(byte[] rawPacket, uint packetLength)
        {
            _packet = rawPacket;
            _length = packetLength;
             TcpStart = (rawPacket[0] & 15) * 5;
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

        private uint _srcIp = 0;
        public uint SrcIp 
        {
            get
            {                
                return 0;                
            }
        }

        private short _srcPort = 0;
        public ushort SrcPort
        { 
            get { return 0; }
        }

        private uint _dstIp = 0;
        public uint DestIp
        {
            get { return 0; }
        }

        private short _dstPort = 0;
        public ushort DestPort
        {
            get { return 0; }
        }

        public bool Fin => (_packet[TcpStart + 13] & 1) == 1;
        public bool Syn => (_packet[TcpStart + 13] & 2) == 1;
        public bool Rst => (_packet[TcpStart + 13] & 4) == 1;
        public bool Ack => (_packet[TcpStart + 13] & 16) == 1;
        public bool SynAck => Syn && Ack;
        public bool FinAck => Fin && Ack;

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

        private string uri = "";
        public bool IsHttpGet()
        {
            return false;
        }

        public bool IsHttpResponse()
        {
            return false;
        }

        public HttpResponseHeaders HttpResponseHeaders
        {
            get { return null; }
        }

        public string Uri
        {
            get
            {
                if (uri.Length == 0)
                {
                    uri = "";
                }

                return uri;
            }
        }
    }
}
