namespace Sentro.Traffic
{
    class SentroHttpRequest : ITcpStreem
    {
        public const string Tag = "SentroHttpRequest";
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
    }
}
