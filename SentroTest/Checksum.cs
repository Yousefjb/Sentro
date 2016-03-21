using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SentroTest
{
    [TestClass]
    public class Checksum
    {
        [TestMethod]
        public void Test_Ip_Checksum()
        {
            byte[] ipheaders = {69, 0, 0, 115, 0, 0, 64, 0, 64, 17, 0, 0, 192, 168, 0, 1, 192, 168, 0, 199};            
            Assert.AreEqual(47201, IpChecksum(ipheaders));
        }

        [TestMethod]
        public void Test_Tcp_Checksum()
        {
            byte[] packet =
            {
                0x45, 0xb8, 0x00, 0x28, 0x2e, 0xe7, 0x00, 0x00, 0x80, 0x06, 0x18, 0x74, 0xad, 0xc7, 0x91, 0x34, 0x0b,
                0x01, 0xa8, 0xc0, //ipheaders
                0x00, 0x50, 0x48, 0x10, 0x09, 0xfe, 0xd2, 0x88, 0xa2, 0xeb, 0x4b, 0x15, 0x50, 0x10, 0x7e, 0x7f, 0x00,
                0x00, 0x00,0x00,0x01//tcp headers
            };
            
            Assert.AreEqual(11184,TcpChecksum(packet));
        }

        public ushort TcpChecksum(byte[] packet)
        {
            uint checksum = 0;

            var rawPacketLegnth = packet.Length;
            bool padZeros = rawPacketLegnth % 2 == 1;
            for (int i = 20; i < rawPacketLegnth; i += 2)
                checksum += ((uint) ((packet[i] << 8) | (padZeros ? 0 : packet[i + 1])));


            for (int i = 12; i < 20; i += 2)
                checksum += ((uint) ((packet[i] << 8) | packet[i + 1]));

            checksum += 6;
            checksum += ((uint)packet.Length - 20);

            var carry = checksum >> 16;
            checksum &= 0xffff;
            while (carry > 0)
            {
                checksum += carry;
                checksum &= 0xffff;
                carry = checksum >> 16;
            }

            checksum = ~checksum;
            return (ushort)checksum;
        }

        public ushort IpChecksum(byte[] ipheaders)
        {
            uint checksum = 0;
            for (int i = 0; i < 20; i += 2)
                checksum += ((uint)((ipheaders[i] << 8) | ipheaders[i + 1]));

            var carry = checksum >> 16;
            checksum &= 0xffff;
            while (carry > 0)
            {
                checksum += carry;
                checksum &= 0xffff;
                carry = checksum >> 16;
            }

            checksum = ~checksum;
            return (ushort)checksum;
        }
    }
}
