using System;
using System.Collections.Generic;
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
        private SortedList<uint,Packet> _packets;        
        public HttpResponseHeaders Headers { get; private set; }
        private int _capturedDataLength;
        private uint _nextExpectedSequence;
        private uint _currentExpectedSequence;
        public CacheManager.Cacheable Cacheable = CacheManager.Cacheable.NotDetermined;

        public SentroResponse()
        {
           Init();
        }

        private void Init()
        {            
            _packets = new SortedList<uint, Packet>(new PacketSequenceComparerOld());                
            _fileLogger = FileLogger.GetInstance();
        }

        public void Add(Packet packet)
        {
            if (Headers == null)
            {
                Headers = ParseHeaders(packet);
                Cacheable = CacheManager.IsCacheable(this);
                _nextExpectedSequence = packet.TcpHeader.SequenceNumber.Reverse();
                _currentExpectedSequence = packet.TcpHeader.SequenceNumber;                
            }
            if (!_packets.ContainsKey(packet.TcpHeader.SequenceNumber))            
                _packets.Add(packet.TcpHeader.SequenceNumber, packet);
            
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
                _fileLogger.Debug(Tag,"packet seq : " + p.TcpHeader.SequenceNumber);
                if (p.TcpHeader.SequenceNumber == _currentExpectedSequence)
                {                    
                    packets.Add(p);
                    _nextExpectedSequence += (uint) p.DataLength;
                    _currentExpectedSequence = _nextExpectedSequence.Reverse();
                    _capturedDataLength += p.DataLength;
                    _fileLogger.Debug(Tag, "next expected seq : " + _nextExpectedSequence);
                }
                else
                    break;                
            }

            foreach (var p in packets)
                _packets.Remove(p.TcpHeader.SequenceNumber);
                        
            return packets;
        }

        public bool Complete => _capturedDataLength == Headers.ContentLength;

        private class PacketSequenceComparerOld : IComparer<uint>
        {
            public int Compare(uint x, uint y)
            {
                if (x > y)
                    return 1;
                if (y < x)
                    return -1;
                return 0;
            }
        }
    }
}
