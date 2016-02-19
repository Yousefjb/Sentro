using System;

namespace Sentro.Traffic
{
    internal class SentroRequest : ITcpStreem
    {
        public const string Tag = "SentroHttpRequest";

        public bool CanHoldMore(int bytesCount)
        {
            throw new NotImplementedException();
        }

        public void Push(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public SentroRequest()
        {
            throw new NotImplementedException();
        }

        public SentroRequest(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public SentroRequest(ref byte[] buffer, int length)
        {
            /*copy buffer here*/
            throw new NotImplementedException();
        }

        public SentroRequest(ITcpStreem streem)
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

        public string RequestUri()
        {
            throw new NotImplementedException();
        }

        public string RequestUriHashed()
        {
            throw new NotImplementedException();
        }
    }
}
