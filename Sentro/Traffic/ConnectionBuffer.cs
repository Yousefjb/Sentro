using System.IO;
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
        private StreamWriter _streamWriter;

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
            }
            else if (_isCacheable)
                _response.Push(bytes, length);
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
            _request.Dispose();
            _response.Dispose();            
        }       
    }
}
