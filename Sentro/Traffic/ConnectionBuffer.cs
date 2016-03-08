using System.IO;
using Divert.Net;
using Sentro.Cache;

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

        public ConnectionBuffer(SentroRequest request)
        {            
            _request = request;    
        }
       
        public void Buffer(byte[] bytes, int length)
        {
            if (_response == null)
            {
                _response = new SentroResponse(bytes, length);
                _isCacheable = CacheManager.IsCacheable(_response);                   
                _fileStream = new FileStream(_request.RequestUriHashed(),FileMode.Append);      
            }
            else if (_isCacheable)
            {                
                _fileStream.Write(bytes,0,length);
                _fileStream.Seek(0, SeekOrigin.End);
                _response.CapturedLength += length - Offset(bytes,length);
            }
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

        protected int Offset(byte[] bytes, int length)
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
