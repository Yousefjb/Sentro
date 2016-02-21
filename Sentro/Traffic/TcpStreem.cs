using System;
using System.Collections.Generic;

namespace Sentro.Traffic
{
    class TcpStreem
    {
        public const string Tag = "TcpStreem";
        protected List<byte[]> Buffer;
        protected int MaxBufferSize;
        protected int CurrentBufferSize;
        protected int TotalSize;        

        protected TcpStreem(byte[] bytes, int length,int maxBufferSize)
        {
            Init(maxBufferSize);
            Push(bytes,length);
        }

        protected TcpStreem(int maxBufferSize)
        {
            Init(maxBufferSize);
        }

        protected void Init(int maxBufferSize)
        {
            Buffer = new List<byte[]>();
            MaxBufferSize = maxBufferSize;
            CurrentBufferSize = 0;
            TotalSize = 0;
        }
        public bool CanHoldMore(int bytesCount)
        {
            return CurrentBufferSize + bytesCount <= MaxBufferSize;
        }

        public void Push(byte[] bytes, int length)
        {
            Push(bytes, 0, length);
        }

        public void Push(byte[] bytes, int startIndex, int length)
        {
            if (bytes.Length == length && startIndex == 0)
                Buffer.Add(bytes);
            else
            {
                var copy = new byte[length];
                Array.Copy(bytes, startIndex, copy, 0, length);
                Buffer.Add(copy);
            }
            CurrentBufferSize += length;
            TotalSize += length;
        }

        public byte[] ToBytes()
        {
            //count * 4 is for the size of each byte array in buffer list
            var bytes = new byte[TotalSize + Buffer.Count * 4];
            int index = 0;
            foreach (var packet in Buffer)
            {
                var intAsBytes = BitConverter.GetBytes(packet.Length);
                Array.Copy(intAsBytes, 0, bytes, index, 4);
                index += 4;
                Array.Copy(packet, 0, bytes, index, packet.Length);
                index += packet.Length;
            }
            return bytes;
        }

        public void Dispose()
        {
            Buffer.Clear();
            Buffer = null;
        }

        public void SetAddressesFrom(TcpStreem streem)
        {
            throw new NotImplementedException();
        }

        public byte[] SourceIp()
        {
            throw new NotImplementedException();
        }

        public void SetSourceIp(byte[] sourceIp)
        {
            throw new NotImplementedException();
        }

        public byte[] SourcePort()
        {
            throw new NotImplementedException();
        }

        public void SetSourcePort(byte[] sourcePort)
        {
            throw new NotImplementedException();
        }

        public byte[] DestinationIp()
        {
            throw new NotImplementedException();
        }

        public void SetDestinationIp(byte[] destinationIp)
        {
            throw new NotImplementedException();
        }

        public byte[] DestinationPort()
        {
            throw new NotImplementedException();
        }

        public void SetDestinationPort(byte[] destinationPort)
        {
            throw new NotImplementedException();
        }
    }
}
