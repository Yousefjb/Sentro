using System;
using System.IO;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : Hold request and response buffers for a connection
     */
    class ConnectionBuffer : IDisposable
    {
        private const string Tag = "ConnectionBuffer";        
        private readonly SentroRequest _request;
        private readonly SentroResponse _response;                
        private FileStream _fileStream;
        private readonly FileLogger _fileLogger;

        public bool LockedForCache { get; set; }
        public long LockedAt { get; set; }

        public ConnectionBuffer(SentroRequest request)
        {            
            _request = request;
            _response = new SentroResponse();
            _fileLogger = FileLogger.GetInstance();
        }

        private void WriteToStream(Packet packet)
        {
            if(_fileStream == null)
                _fileStream = CacheManager.OpenFileWriteStream(_request.RequestUriHashed());

            var orderedPackets = _response.GetOrderedPackets();
            _fileLogger.Debug(Tag,"ordered packet to write : " + orderedPackets.Count);
            foreach (var orderedPacket in orderedPackets)
            {
                _fileLogger.Debug(Tag,"seq : " + orderedPacket.TcpHeader.SequenceNumber.Reverse());
                _fileStream.Write(orderedPacket.Data, 0, orderedPacket.DataLength);
                _fileStream.Seek(0, SeekOrigin.End);
                _fileStream.Flush();
            }
        }

        public void AddResponsePacket(Packet packet)
        {
            try
            {
                _fileLogger.Debug(Tag,"in packet");
                switch (_response.Cacheable)
                {
                    case CacheManager.Cacheable.Yes:
                        _response.Add(packet);
                        WriteToStream(packet);
                        break;
                    case CacheManager.Cacheable.NotDetermined:
                        _response.Add(packet);
                        if (_response.Cacheable == CacheManager.Cacheable.Yes)
                            WriteToStream(packet);
                        break;
                }
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag, e.ToString());
            }
        }

        static int SearchBytes(byte[] haystack,int haystackLength, byte[] needle)
        {
            var len = needle.Length;
            var limit = haystackLength - len;
            for (var i = 0; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

        public bool ResponseCompleted
        {
            get
            {
                if(_response.Complete)
                    Reset();
                return _response.Complete;
            }
        }
              
        private void Reset()
        {
            if (_fileStream == null)
                return;

            _fileStream.Flush();
            _fileStream.Close();            
        }

        public void Dispose()
        {
            Reset();            
        }        
    }
}
