using System.Collections.Generic;
using System.IO;
using System.Threading;
using Sentro.Traffic;
using Sentro.Utilities;

namespace Sentro.Cache
{
    public class CacheResponse
    {
        private readonly FileStream _fileStream;
        private FileLogger fileLogger;
        private bool _closed;
        private SemaphoreSlim nextPacketSem;
        public CacheResponse(FileStream fs)
        {
            _fileStream = fs;            
            fileLogger = FileLogger.GetInstance();
            nextPacketSem = new SemaphoreSlim(1, 1);
        }

        public void Close()
        {
            _closed = true;
            _fileStream.Dispose();            
        }
        
        public IEnumerable<Packet> NetworkPackets
        {
            get
            {                
                long length = _fileStream.Length;
                long read = 0;
                var headersPacket = HeadersPacket();
                yield return headersPacket;
                read += headersPacket.DataLength;
                while (read < length && !_closed)
                {
                    byte[] rawPacket = new byte[1500];
                    var stepRead = _fileStream.Read(rawPacket, 40, 1420);
                    read += stepRead;
                    fileLogger.Debug("netowrkPacket", "read : " + read);
                    yield return new Packet(rawPacket, (uint) stepRead + 40);
                }
            }
        }

        
        private Packet HeadersPacket()
        {
            var firstByte = _fileStream.ReadByte();
            var secondByte = _fileStream.ReadByte();
            var headersLength = (firstByte << 8) | secondByte;
            byte[] headersPacket = new byte[1500];
            _fileStream.Read(headersPacket, 40, headersLength);
            return new Packet(headersPacket, (uint) headersLength + 40);
        }

        private IEnumerator<Packet> enumerator;      
        public Packet NextPacket()
        {
            nextPacketSem.Wait();
            if (enumerator == null)
                enumerator = NetworkPackets.GetEnumerator();
            Packet nextPacket = null;
            if (enumerator.MoveNext())
            {
                fileLogger.Debug("netowrkPacket", "i found next packet");         
                nextPacket = enumerator.Current;
            }
            nextPacketSem.Release();        
            return nextPacket;
        }
      
    }
}
