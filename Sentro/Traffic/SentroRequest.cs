using System;

namespace Sentro.Traffic
{
    class SentroRequest : ITcpStreem
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
        public SentroRequest(ref byte[] buffer,int length)
        {
            /*copy buffer here*/
            throw new NotImplementedException();
        }

        public void Push(ref byte[] buffer, uint length)
        {
            throw new NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public string RequestUri()
        {
            throw new NotImplementedException();
        }
    }
}
