namespace Sentro.Traffic
{
    interface ITcpStreem
    {
        bool CanHoldMore(int bytesCount);
        void Push(byte[] buffer);
        void Push(ref byte[] buffer,int length);   
        byte[] ToBytes();
        void Dispose();
    }
}
