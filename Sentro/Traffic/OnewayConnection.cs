namespace Sentro.Traffic
{
    public class OnewayConnection
    {
        private uint SEQ, ACK;
        private TcpState tcpState;
        private uint srcIp, destIp;
        private ushort srcPort, destPort;
        public OnewayConnection(uint SrcIp, uint DestIp, ushort SrcPort, ushort DestPort)
        {
            this.srcPort = SrcPort;
            this.destPort = DestPort;
            this.srcIp = SrcIp;
            this.destIp = DestIp;
            tcpState = TcpState.Closed;
        }

        public void SendItA(Packet packet)
        {

        }

        public TcpState CurrnetTcpState => tcpState;

        public int Identity()
        {
            return 0;
        }

        public uint SrcIp => srcIp;
        public uint DestIp => destIp;
        public ushort SrcPort => srcPort;
        public ushort DestPort => destPort;

        public uint SeqNumber => SEQ;

        public uint AckNumber
        {
            get { return ACK; }
            set { ACK = value; }
        }

        public enum TcpState
        {
            Closed,
            SynRcvd,
            Listen,
            SynSent,
            Established,
            CloseWait,
            LastAck,
            FinWait1,
            FinWait2,
            TimeWait,
            Closing,

            SendingCache,
            Caching
        }
    }
}
