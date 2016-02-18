namespace Sentro.Traffic
{
    class SentroHttpResponse : ITcpStreem
    {
        public const string Tag = "SentroHttpResponse";
        public bool CanHoldMore(int bytesCount)
        {
            throw new System.NotImplementedException();
        }

        public void Push(byte[] buffer)
        {
            throw new System.NotImplementedException();
        }

        public void Push(ref byte[] buffer, uint length)
        {
            throw new System.NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new System.NotImplementedException();
        }


        public SentroHttpResponse()
        {
        }

        public SentroHttpResponse(byte[] buffer, int length)
        {

        }
    }
}
