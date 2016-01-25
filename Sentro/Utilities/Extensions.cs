using System;
using System.Collections.ObjectModel;
using PcapDotNet.Base;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;

namespace Sentro.Utilities
{
    static class Extensions
    {
        public static ReadOnlyCollection<byte> ToBytes(this MacAddress mac)
        {
            var hexValues = mac.ToString().Split(':');
            if (hexValues.Length != 6)
                throw new InvalidCastException($"{mac} is not a valid mac address");

            var macBytes = new byte[6];
            for (int i = 0; i < hexValues.Length; i++)
                macBytes[i] = Convert.ToByte(hexValues[i], 16);
            return macBytes.AsReadOnly();
        }

        public static ReadOnlyCollection<byte> ToBytes(this IpV4Address ip)
        {
            var hexValues = ip.ToString().Split('.');
            if (hexValues.Length != 4)
                throw new InvalidCastException($"{ip} is not a valid ip address");

            var ipBytes = new byte[4];
            for (int i = 0; i < hexValues.Length; i++)
                ipBytes[i] = Convert.ToByte(hexValues[i]);
            return ipBytes.AsReadOnly();
        }

    }
}
