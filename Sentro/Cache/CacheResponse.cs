using System.Collections.Generic;
using System.IO;
using Divert.Net;
using Sentro.Traffic;
using Sentro.Utilities;

namespace Sentro.Cache
{
    public class CacheResponse
    {
        private readonly FileStream _fileStream;
        private FileLogger fileLogger;
        private bool _closed;      
        public CacheResponse(FileStream fs)
        {
            _fileStream = fs;
            fileLogger = FileLogger.GetInstance();
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
                    fileLogger.Debug("netowrkPacket","read : " +read);
                    yield return new Packet(rawPacket, (uint) stepRead + 40);                    
                }
            }
        }

        private IEnumerator<Packet> enumerator;
        public Packet NextPacket()
        {
            if (enumerator == null)
                enumerator = NetworkPackets.GetEnumerator();
            if (enumerator.MoveNext())
            {
                fileLogger.Debug("netowrkPacket", "i found next packet");
                return enumerator.Current;
            }
            return null;
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
