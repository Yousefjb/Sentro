using System;
using System.Collections.Generic;

namespace Sentro.Traffic
{
    class SentroResponse : ITcpStreem
    {
        public const string Tag = "SentroHttpResponse";
        private List<byte[]> _buffer;
        private int _maxBufferSize;
        private int _currentBufferSize;
        private int _totalSize;

        public SentroResponse(byte[] bytes, int length)
        {
            Init();
            Push(bytes, length);
        }

        public SentroResponse()
        {
            Init();
        }

        private void Init()
        {
            _buffer = new List<byte[]>();
            _maxBufferSize = 5242880;//5MB TODO: load from settings
            _currentBufferSize = 0;
            _totalSize = 0;
        }


        public bool CanHoldMore(int bytesCount)
        {
            return _currentBufferSize + bytesCount <= _maxBufferSize;
        }

        public void Push(byte[] bytes, int length)
        {
            if (bytes.Length == length)
                _buffer.Add(bytes);
            else
            {
                var copy = new byte[length];
                Array.Copy(bytes, copy, length);
                _buffer.Add(copy);
            }
            _currentBufferSize += length;
            _totalSize += length;
        }

        public byte[] ToBytes()
        {
            var bytes = new byte[_totalSize + _buffer.Count * 4];
            int index = 0;
            foreach (var packet in _buffer)
            {
                Array.Copy(packet, 0, bytes, index, packet.Length);
                index += packet.Length;
            }
            return bytes;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void MatchFor(SentroRequest request)
        {
            throw new NotImplementedException();
        }

        public List<byte[]> Packets()
        {
            return _buffer;
        }
    }
}
