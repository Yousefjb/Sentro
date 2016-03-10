using System;
using System.IO;
using Divert.Net;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : Hold request and response buffers for a connection
     */
    class ConnectionBuffer
    {
        public const string Tag = "ConnectionBuffer";        
        private SentroRequest _request;
        private SentroResponse _response;        
        private bool _isCacheable;        
        private FileStream _fileStream;
        private FileLogger _fileLogger;    

        public ConnectionBuffer(SentroRequest request)
        {            
            _request = request;    
        }
       
        public void Buffer(byte[] bytes, int length)
        {
            if (_response == null)
            {
                _fileLogger = FileLogger.GetInstance();
                var path = _request.RequestUriHashed();
                path = FileHierarchy.GetInstance().MapToFilePath(path);
                _response = new SentroResponse(bytes, length);
                _isCacheable = CacheManager.IsCacheable(_response);
                _fileStream = new FileStream(path, FileMode.Append);

                var offset = Offset(bytes, length);
                //var endOfHeaders = SearchBytes(bytes, length, new byte[] {0x0D, 0x0A, 0x0D, 0x0A});
                //if(endOfHeaders < 0)
                //    return;
                //endOfHeaders += 4;

                _fileStream.Write(bytes, offset, length - offset);
                _fileStream.Seek(0, SeekOrigin.End);
            }
            else if (_isCacheable)
            {
                var offset = Offset(bytes, length);
                _fileStream.Write(bytes, offset, length - offset);
                _fileStream.Seek(0, SeekOrigin.End);
                _response.CapturedLength += length - offset;
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

        private int Offset(byte[] bytes, int length)
        {
            TCPHeader tcpHeader;
            IPHeader ipHeader;
            TrafficManager.GetInstance().Parse(bytes, (uint) length, out tcpHeader, out ipHeader);
            return HelperFunctions.Offset(tcpHeader, ipHeader);
        }

        private void Reset()
        {
            if (_fileStream == null)
                return;

            _fileStream.Flush();
            _fileStream.Close();
        }       
    }
}
