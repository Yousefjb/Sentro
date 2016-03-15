using System;
using System.Collections.ObjectModel;
using System.Text;
using PcapDotNet.Base;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;

namespace Sentro.Utilities
{
    /*
        Responsibility : extra functions to improve writablity
    */
    static class Extensions
    {
        public const string Tag = "Extensions";

        public static ReadOnlyCollection<byte> ToBytes(this MacAddress mac)
        {
            var x = mac.ToValue();
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

        public static uint Reverse(this uint number)
        {
            var bytes = BitConverter.GetBytes(number);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static string MurmurHash(this string url)
        {
            return Murmur2.HashX8(url,Encoding.ASCII);
        }

        public static string NormalizeUri(this string url)
        {
            url = new UriBuilder(url).Uri.ToString();

            var i = url.IndexOf("#", StringComparison.Ordinal);
            if (i != -1)
                url = url.Remove(i);

            url = url.Replace("//", "/");

            url = url.Replace("www.", "");

            // Sorting query parameters
            var queryString = url.Substring(url.IndexOf('?') + 1).Split('&');
            Array.Sort(queryString);

            var builder = new StringBuilder();
            builder.Append(url.Substring(0, url.IndexOf('?') + 1));

            foreach (var value in queryString)
            {
                builder.Append(value);
                builder.Append('&');
            }
            builder.Remove(builder.Length - 1, 1);
            url = builder.ToString();
            return url;
        }

    }
}
