using System;

namespace Sentro.Traffic
{
    //Named Quick to no conflict with Pcap.Net Packet Builder
    public class QuickPacketBuilder
    {
        private const int SYNACK_SIZE = 52;

        private OnewayConnection connection;

        //Need a OnewayConenction to use while building packets
        public QuickPacketBuilder(OnewayConnection connection)
        {
            this.connection = connection;
        }

        //Get SynAck Packet and increase the ACK number by one
        public IncompletePacket SynAck()
        {
            var synAck = new IncompletePacket(SYNACK_SIZE);
            connection.AckNumber++;
            SetIpLayer(synAck);
            SetTcpLayer(synAck, new byte[] {2, 4, 5, 0x8c, 1, 1, 4, 2, 1, 3, 3, 7});
            return synAck;
        }
      
        private void SetIpLayer(IncompletePacket packet)
        {
            var bytes = packet.Bytes;
            bytes[0] = 69;                        
            bytes[1] = 184;

            bytes[2] = (byte) ((bytes.Length >> 8) & 0xff);
            bytes[3] = (byte) (bytes.Length & 0xff);

            var id = connection.Identity();
            bytes[4] = (byte)((id >> 8) & 0xff);
            bytes[5] = (byte)(id & 0xff);
            
            bytes[6] = 0;
            bytes[7] = 0;            
            bytes[8] = 128;            
            bytes[9] = 6;

            bytes[10] = 0;
            bytes[11] = 0;

            var srcIp = connection.SrcIp;
            bytes[12] = (byte) (srcIp >> 24);
            bytes[13] = (byte) ((srcIp >> 16) & 0xff);
            bytes[14] = (byte) ((srcIp >> 8) & 0xff);
            bytes[15] = (byte) (srcIp & 0xff);

            var destIp = connection.DestIp;
            bytes[16] = (byte) (destIp >> 24);
            bytes[17] = (byte) ((destIp >> 16) & 0xff);
            bytes[18] = (byte) ((destIp >> 8) & 0xff);
            bytes[19] = (byte) (destIp & 0xff);
        }

        private void SetTcpLayer(IncompletePacket packet,byte[] options)
        {
            var bytes = packet.Bytes;
            var tcpStartIndex = (bytes[0] & 15)*4;
            var index = tcpStartIndex;

            var srcPort = connection.SrcPort;
            bytes[index++] = (byte) ((srcPort >> 8) & 0xff);
            bytes[index++] = (byte) (srcPort & 0xff);

            var destPort = connection.DestPort;
            bytes[index++] = (byte)((destPort >> 8) & 0xff);
            bytes[index++] = (byte)(destPort & 0xff);

            uint seqNumber;
            if (connection.CurrnetTcpState == OnewayConnection.TcpState.Closed)
                seqNumber = (uint) (new Random().Next()%uint.MaxValue);
            else seqNumber = connection.SeqNumber;

            bytes[index++] = (byte)(seqNumber >> 24);
            bytes[index++] = (byte)((seqNumber >> 16) & 0xff);
            bytes[index++] = (byte)((seqNumber >> 8) & 0xff);
            bytes[index++] = (byte)(seqNumber & 0xff);

            uint ackNumber;
            if (connection.CurrnetTcpState == OnewayConnection.TcpState.Closed)
                ackNumber = (uint) (new Random().Next()%uint.MaxValue);
            else ackNumber = connection.AckNumber;

            bytes[index++] = (byte)(ackNumber >> 24);
            bytes[index++] = (byte)((ackNumber >> 16) & 0xff);
            bytes[index++] = (byte)((ackNumber >> 8) & 0xff);
            bytes[index++] = (byte)(ackNumber & 0xff);

            bytes[index++] = 80;

            bytes[index++] = 18;
                      
            bytes[index++] = (32383 >> 8) & 0xff;
            bytes[index++] = 32383 & 0xff;


            bytes[index++] = 0;
            bytes[index++] = 0;

            bytes[index++] = 0;
            bytes[index++] = 0;
            for (int i = 0; i < options.Length; i++)
                bytes[index + i] = options[i];
        }

        public class IncompletePacket
        {
            private byte[] bytes;
            public IncompletePacket(int size)
            {
                bytes = new byte[size];
            }

            public Packet Build()
            {
                return new Packet(bytes, (uint) bytes.Length);
            }

            public byte[] Bytes => bytes;
        }
    }
}
