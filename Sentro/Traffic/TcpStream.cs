using System;
using System.Collections.Generic;
using System.Text;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : hold data for http stream
        TODO : Use direct access to TcpHeader and IpHeader to modify source and dest addresses
        TODO : remove this class since request doesn't need a stream ( single packet )
    */
    class TcpStream
    {
        public const string Tag = "TcpStreem";
        protected List<byte[]> Buffer;
        private int _totalSize;
        private TCPHeader _tcpHeader;
        private IPHeader _ipHeader;
        private static FileLogger _fileLogger;           

        protected TcpStream(byte[] bytes, int length,int maxBufferSize)
        {
            Init(maxBufferSize);
            Push(bytes,length);
        }

        protected TcpStream(int maxBufferSize)
        {
            Init(maxBufferSize);
        }

        private void Init(int maxBufferSize)
        {
            Buffer = new List<byte[]>();
            _totalSize = 0;
            _fileLogger = FileLogger.GetInstance();
        }

        protected void Push(byte[] bytes, int length)
        {
            Push(bytes, 0, length);
        }

        private void Push(byte[] bytes, int startIndex, int length)
        {
            try
            {
                if (bytes.Length == length && startIndex == 0)
                    Buffer.Add(bytes);
                else
                {
                    var copy = new byte[length];
                    Array.Copy(bytes, startIndex, copy, 0, length);
                    Buffer.Add(copy);
                }                
                _totalSize += length;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());      
            }            
        }

        public byte[] ToBytes()
        {
            try
            {                              
                _fileLogger.Debug(Tag,$"converting response {Buffer.Count} packet's to bytes");
                //count * 4 is for the size of each byte array in buffer list                
                var bytes = new byte[_totalSize + Buffer.Count*4];
                int index = 0;
                foreach (var packet in Buffer)
                {
                    var intAsBytes = BitConverter.GetBytes(packet.Length);
                    Array.Copy(intAsBytes, 0, bytes, index, 4);                    
                    index += 4;
                    Array.Copy(packet, 0, bytes, index, packet.Length);                    
                    index += packet.Length;
                    _fileLogger.Debug(Tag,Encoding.UTF8.GetString(packet));
                }
                return bytes;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
                return null;
            }
        }

        protected void LoadFrom(byte[] bytes, int length)
        {
            try
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
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
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
            if (_ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _ipHeader?.SourceAddress.GetAddressBytes();
        }

        protected void SetSourceIp(byte[] sourceIp)
        {
            ReplaceInAll(sourceIp,12);
        }

        public byte[] SourcePort()
        {
            if (_tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _tcpHeader != null ? BitConverter.GetBytes(_tcpHeader.SourcePort) : null;
        }

        protected void SetSourcePort(byte[] sourcePort)
        {
            foreach (byte[] packet in Buffer)
            {
                var tcpStartPos = BitConverter.ToInt16(packet, 2) + 20;
                Replace(sourcePort, 0, packet, tcpStartPos);
            }
        }

        public byte[] DestinationIp()
        {
            if (_ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint)Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _ipHeader?.DestinationAddress.GetAddressBytes();
        }

        protected void SetDestinationIp(byte[] destinationIp)
        {
            ReplaceInAll(destinationIp, 16);
        }

        public byte[] DestinationPort()
        {
            if (_tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _tcpHeader != null ? BitConverter.GetBytes(_tcpHeader.DestinationPort) : null;
        }

        protected void SetDestinationPort(byte[] destinationPort)
        {
            foreach (byte[] packet in Buffer)
            {
                var tcpStartPos = BitConverter.ToInt16(packet, 2) + 22;
                Replace(destinationPort, 0, packet, tcpStartPos);
            }
        }

        protected int Offset()
        {
            if (_tcpHeader == null || _ipHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint) Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return HelperFunctions.Offset(_tcpHeader, _ipHeader);
        }

        protected int Offset(byte[] bytes,uint length)
        {
            if (_tcpHeader == null || _ipHeader == null)
                TrafficManager.GetInstance().Parse(bytes,length, out _tcpHeader, out _ipHeader);
            return HelperFunctions.Offset(_tcpHeader, _ipHeader);
        }
    }
}
