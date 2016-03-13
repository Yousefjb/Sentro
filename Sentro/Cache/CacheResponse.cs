using System.Collections.Generic;
using System.IO;
using Divert.Net;
using Sentro.Traffic;

namespace Sentro.Cache
{
    public class CacheResponse
    {
        private readonly FileStream _fileStream;
        public CacheResponse(FileStream fs)
        {
            _fileStream = fs;
        }

        public void Close()
        {
            _fileStream.Close();            
        }

        public IEnumerable<Packet> NetworkPackets
        {
            get
            {
                long length = _fileStream.Length;
                long read = 0;
                while (read < length)
                {
                    byte[] rawPacket = new byte[1500];
                    var stepRead = _fileStream.Read(rawPacket, 40, 1460);
                    read += stepRead;
                    yield return new Packet(rawPacket, (uint)stepRead, new TCPHeader(), new IPHeader());
                }                             
            }
        }
    }
}
