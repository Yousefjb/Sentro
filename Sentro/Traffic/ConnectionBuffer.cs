using System;

namespace Sentro.Traffic
{
    class ConnectionBuffer
    {
        public const string Tag = "ConnectionBuffer";        
        private SentroRequest _request;
        private SentroResponse _response;
        private bool _tempCreated;

        public ConnectionBuffer(SentroRequest request)
        {            
            _request = request;
            _response = new SentroResponse();
        }

        public void Buffer(byte[] bytes, int length)
        {
            if (!_response.CanHoldMore(length))
            {
                if (!_tempCreated)
                    Flush(_request);

                Flush(_response);
                _tempCreated = true;
            }

            _response.Push(bytes,length);
        }

        public SentroRequest Request()
        {
            return _request;
        }

        public SentroResponse Response()
        {
            return _response;
        }

        public void Reset()
        {
            _request.Dispose();
            _response.Dispose();            
        }

        private void Flush(TcpStreem streem)
        {
            throw new NotImplementedException();
        }
    }
}
