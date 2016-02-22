using System;
using System.Collections.Generic;
using Divert.Net;

namespace Sentro.Traffic
{
    class TcpStreem
    {
        public const string Tag = "TcpStreem";
        protected List<byte[]> Buffer;
        protected int MaxBufferSize;
        protected int CurrentBufferSize;
        protected int TotalSize;
        private TCPHeader tcpHeader;
        private IPHeader ipHeader;             

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

        protected void LoadFrom(byte[] bytes, int length)
        {
            int index = 0;
            while (index < length)
            {
                var packetLength = BitConverter.ToInt32(bytes, index);
                index += 4;
                Push(bytes, index, packetLength);
                index += packetLength;
            }
        }

        public void Dispose()
        {
            Buffer.Clear();
            Buffer = null;
        }

        private void ReplaceInAll(byte[] bytes, int startIndex)
        {
            for (int i = 0; i < Buffer.Count; i++)
            {
                for (int j = 0; j < bytes.Length; j++)
                    Buffer[0][startIndex + j] = bytes[j];
            }
        }

        private void Replace(byte[] source, int sourceStart, byte[] target, int targetStart)
        {
            var count = source.Length - sourceStart;
            for (int i = 0; i < count; i++)
            {
                target[targetStart + i] = source[sourceStart + i];
            }
        }

        public byte[] SourceIp()
        {
            if (ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out tcpHeader, out ipHeader);
            return ipHeader?.SourceAddress.GetAddressBytes();
        }

        public void SetSourceIp(byte[] sourceIp)
        {
            ReplaceInAll(sourceIp,12);
        }

        public byte[] SourcePort()
        {
            if (tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out tcpHeader, out ipHeader);
            return tcpHeader != null ? BitConverter.GetBytes(tcpHeader.SourcePort) : null;
        }

        public void SetSourcePort(byte[] sourcePort)
        {
            foreach (byte[] packet in Buffer)
            {
                var tcpStartPos = BitConverter.ToInt16(packet, 2) + 20;
                Replace(sourcePort, 0, packet, tcpStartPos);
            }
        }

        public byte[] DestinationIp()
        {
            if (ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint)Buffer[0].Length, out tcpHeader, out ipHeader);
            return ipHeader?.DestinationAddress.GetAddressBytes();
        }

        public void SetDestinationIp(byte[] destinationIp)
        {
            ReplaceInAll(destinationIp, 16);
        }

        public byte[] DestinationPort()
        {
            if (tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out tcpHeader, out ipHeader);
            return tcpHeader != null ? BitConverter.GetBytes(tcpHeader.DestinationPort) : null;
        }

        public void SetDestinationPort(byte[] destinationPort)
        {
            foreach (byte[] packet in Buffer)
            {
                var tcpStartPos = BitConverter.ToInt16(packet, 2) + 22;
                Replace(destinationPort, 0, packet, tcpStartPos);
            }
        }

        public int Offset()
        {
            if (tcpHeader == null || ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out tcpHeader, out ipHeader);
            return HelperFunctions.Offset(tcpHeader, ipHeader);
        }
    }
}
