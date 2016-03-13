using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : TcpStream that hold response bytes with http response specific functions        
    */
    internal class SentroResponse
    {
        private const string Tag = "SentroResponse";
        private FileLogger _fileLogger;                        
        private SortedList<Packet,Packet> _packets;        
        public HttpResponseHeaders Headers { get; private set; }
        private int _capturedDataLength;
        private uint _nextExpectedPacket;
        public CacheManager.Cacheable Cacheable = CacheManager.Cacheable.NotDetermined;

        public SentroResponse()
        {
           Init();
        }

        private void Init()
        {            
            _packets = new SortedList<Packet, Packet>();            
            _fileLogger = FileLogger.GetInstance();
        }

        public void Add(Packet packet)
        {
            if (Headers == null)
            {
                Headers = ParseHeaders(packet);
                Cacheable = CacheManager.IsCacheable(this);
                _nextExpectedPacket = packet.TcpHeader.SequenceNumber.Reverse() + (uint)packet.DataLength;
            }
            if (!_packets.ContainsKey(packet))
                _packets.Add(packet, packet);
        }

        private HttpResponseHeaders ParseHeaders(Packet packet)
        {
            var headers = new HttpResponseHeaders();
            var ascii = Encoding.ASCII.GetString(packet.Data);
            var result = Regex.Match(ascii, CommonRegex.HttpContentLengthMatch,
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            headers.ContentLength = result.Success ? Convert.ToInt32(result.Groups[1].Value) : 0;
            return headers;
        }    

        public List<Packet> GetOrderedPackets()
        {
            var enume = _packets.GetEnumerator();
            var packets = new List<Packet>();
            while (enume.MoveNext())
            {
                var p = enume.Current.Value;
                if (p.TcpHeader.SequenceNumber.Reverse() <= _nextExpectedPacket)
                {                    
                    packets.Add(p);
                    _nextExpectedPacket += (uint) p.DataLength;
                    _capturedDataLength += p.DataLength;
                }
            }

            foreach (var p in packets)
                _packets.Remove(p);
                        
            return packets;
        }

        public bool Complete => _capturedDataLength == Headers.ContentLength;                    

        public class HttpResponseHeaders
        {
            public int ContentLength { get; set; }        
        }
    }
}
