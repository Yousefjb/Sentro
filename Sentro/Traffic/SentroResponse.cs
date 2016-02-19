using System;
using System.Collections.Generic;

namespace Sentro.Traffic
{
    class SentroResponse : ITcpStreem
    {
        public const string Tag = "SentroHttpResponse";
        public bool CanHoldMore(int bytesCount)
        {
            throw new NotImplementedException();
        }

        public void Push(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public void Push(ref byte[] buffer, int length)
        {
            throw new NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public SentroResponse()
        {
            throw new NotImplementedException();
        }

        public SentroResponse(byte[] buffer, int length)
        {
            throw new NotImplementedException();
        }
    }
}
