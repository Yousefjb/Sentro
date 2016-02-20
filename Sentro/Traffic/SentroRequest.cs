using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    internal class SentroRequest : ITcpStreem
    {
        public const string Tag = "SentroHttpRequest";
        private List<byte[]> _buffer;
        private int _maxBufferSize;
        private int _currentBufferSize;
        private int _totalSize;
        private string requestUri = "";

        public bool CanHoldMore(int bytesCount)
        {
            return _currentBufferSize + bytesCount <= _maxBufferSize;
        }

        public SentroRequest()
        {
            Init();
        }

        public SentroRequest(byte[] bytes, int length)
        {
            Init();
            Push(bytes,length);
        }

        private void Init()
        {
            _buffer = new List<byte[]>();
            _maxBufferSize = 2048;//2KB TODO: load from settings
            _currentBufferSize = 0;
            _totalSize = 0;
        }

        public SentroRequest(ITcpStreem streem)
        {
            throw new NotImplementedException();
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
            var bytes = new byte[_totalSize + _buffer.Count*4];
            int index = 0;
            foreach (var packet in _buffer)
            {
                Array.Copy(packet,0,bytes,index,packet.Length);
                index += packet.Length;
            }
            return bytes;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string RequestUri()
        {
            if (requestUri.Length != 0)
                return requestUri;

            var ascii = Encoding.ASCII.GetString(_buffer[0]);
            var result = Regex.Match(ascii, CommonRegex.HttpGetUriMatch);
            string path = result.Value;
            string host = result.NextMatch().Value;
            requestUri = host + path;
            return requestUri;
        }

        public string RequestUriHashed()
        {
            return new Murmur2().Hash(Encoding.ASCII.GetBytes(RequestUri()));
        }
    }
}
