using Divert.Net;

namespace Sentro.Traffic
{
    class ConnectionController
    {
        private OnewayConnection client, origin;
        private int hashCode;
        private Diversion diversion;
        private QuickPacketBuilder clientPB, originPB;
        public ConnectionController(Packet initiatePacket, Diversion diversion)
        {
            client = new OnewayConnection(initiatePacket.SrcIp, initiatePacket.DestIp,
                                          initiatePacket.SrcPort, initiatePacket.DestPort);

            origin = new OnewayConnection(initiatePacket.DestIp, initiatePacket.SrcIp,
                                          initiatePacket.DestPort, initiatePacket.SrcPort);

            hashCode = initiatePacket.GetHashCode();
            this.diversion = diversion;
            clientPB = new QuickPacketBuilder(client);
            originPB = new QuickPacketBuilder(origin);
        }

        public void Push(Packet packet)
        {
            if (packet.DestPort == 80)
                handleToOriginPacket(packet);
            else handleToClientPacket(packet);
        }

        private void handleToClientPacket(Packet packet)
        {

        }

        private void handleToOriginPacket(Packet packet)
        {
            if (isClientStartingANewConnection(packet))
            {
                var synAck = originPB.SynAck().Build();
                client.SendItA(synAck);
            }


        }



        private bool isClientStartingANewConnection(Packet packet)
        {
            return (client.CurrnetTcpState == OnewayConnection.TcpState.Closed) && packet.Syn;
        }        
    }
}
