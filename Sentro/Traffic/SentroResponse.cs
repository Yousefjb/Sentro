using System;
using System.Collections.Generic;
using System.Text;
using Divert.Net;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : TcpStream that hold response bytes with http response specific functions
        TODO : break down TCP stream functionality to Sentro Response
    */
    internal class SentroResponse
    {
        public new const string Tag = "SentroResponse";

        private int _contentLength, _capturedLength, _totalSize;        
        private FileLogger _fileLogger;
        public int CapturedLength;
        private List<byte[]> Buffer;
        private TCPHeader _tcpHeader;
        private IPHeader _ipHeader;

        public SentroResponse(byte[] bytes, int length)            
        {                     
            _contentLength = HttpParser.ContentLength(bytes, length);
            _fileLogger = FileLogger.GetInstance();            
        }

        public SentroResponse()
        {
            Buffer = new List<byte[]>();
            _fileLogger = FileLogger.GetInstance();
        }      

        public List<byte[]> Packets() => Buffer;

        public bool Complete => CapturedLength == _contentLength;

        public static SentroResponse CreateFromBytes(byte[] bytes, int length)
        {
            var response = new SentroResponse();
            response.LoadFrom(bytes,length);         
            return response;
        }

        public void SetAddressesFrom(SentroRequest request)
        {                      
            SetDestinationIp(request.DestinationIp());
            SetDestinationPort(request.DestinationPort());
            SetSourceIp(request.SourceIp());
            SetSourcePort(request.SourcePort());
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
                _fileLogger.Error(Tag, e.ToString());
            }
        }

        public byte[] ToBytes()
        {
            try
            {
                _fileLogger.Debug(Tag, $"converting response {Buffer.Count} packet's to bytes");
                //count * 4 is for the size of each byte array in buffer list                
                var bytes = new byte[_totalSize + Buffer.Count * 4];
                int index = 0;
                foreach (var packet in Buffer)
                {
                    var intAsBytes = BitConverter.GetBytes(packet.Length);
                    Array.Copy(intAsBytes, 0, bytes, index, 4);
                    index += 4;
                    Array.Copy(packet, 0, bytes, index, packet.Length);
                    index += packet.Length;
                    _fileLogger.Debug(Tag, Encoding.UTF8.GetString(packet));
                }
                return bytes;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag, e.ToString());
                return null;
            }
        }

        public void LoadFrom(byte[] bytes, int length)
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
                _fileLogger.Error(Tag, e.ToString());
            }
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
                TrafficManager.GetInstance().Parse(Buffer[0], (uint)Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _ipHeader?.SourceAddress.GetAddressBytes();
        }

        private void SetSourceIp(byte[] sourceIp)
        {
            ReplaceInAll(sourceIp, 12);
        }

        public byte[] SourcePort()
        {
            if (_tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint)Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _tcpHeader != null ? BitConverter.GetBytes(_tcpHeader.SourcePort) : null;
        }

        private void SetSourcePort(byte[] sourcePort)
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

        private void SetDestinationIp(byte[] destinationIp)
        {
            ReplaceInAll(destinationIp, 16);
        }

        public byte[] DestinationPort()
        {
            if (_tcpHeader == null)
                TrafficManager.GetInstance().Parse(Buffer[0], (uint)Buffer[0].Length, out _tcpHeader, out _ipHeader);
            return _tcpHeader != null ? BitConverter.GetBytes(_tcpHeader.DestinationPort) : null;
        }

        private void SetDestinationPort(byte[] destinationPort)
        {
            foreach (byte[] packet in Buffer)
            {
                var tcpStartPos = BitConverter.ToInt16(packet, 2) + 22;
                Replace(destinationPort, 0, packet, tcpStartPos);
            }
        }
    }
}
