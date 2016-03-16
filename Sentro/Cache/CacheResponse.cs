using System.Collections.Generic;
using System.IO;
using Divert.Net;
using Sentro.Traffic;

namespace Sentro.Cache
{
    public class CacheResponse
    {
        private readonly FileStream _fileStream;
        private bool _closed;      
        public CacheResponse(FileStream fs)
        {
            _fileStream = fs;
        }

        public void Close()
        {
            _closed = true;
            _fileStream.Close();
        }

        public IEnumerable<Packet> NetworkPackets
        {
            get
            {
                long length = _fileStream.Length;
                long read = 0;
                while (read < length && !_closed)
                {
                    byte[] rawPacket = new byte[1500];
                    var stepRead = _fileStream.Read(rawPacket, 40, 1420);
                    read += stepRead;
                    yield return new Packet(rawPacket, (uint) stepRead + 40);                    
                }
            }
        }

        public IEnumerable<Packet> MissedPacket(params int[] packetNumber)
        {
            foreach (int i in packetNumber)
            {
                var packetPos = i*1460;
                _fileStream.Position = packetPos;
                byte[] rawPacket = new byte[1500];
                var readBytes = _fileStream.Read(rawPacket, 40, 1460);
                yield return new Packet(rawPacket, (uint) readBytes + 40);
            }
        }
    }
}
