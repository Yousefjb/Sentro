using System;
using System.Linq;
using System.Net;
using Divert.Net;

namespace Sentro.Traffic
{
    public class Packet : IComparable<Packet>
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

        public int CompareTo(Packet other)
        {

            if (TcpHeader.SequenceNumber > other.TcpHeader.SequenceNumber)
                return 1;
            if (TcpHeader.SequenceNumber < other.TcpHeader.SequenceNumber)
                return -1;
            return 0;
        }
    }
}
