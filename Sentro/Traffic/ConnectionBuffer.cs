using System;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    class ConnectionBuffer
    {
        public const string Tag = "ConnectionBuffer";
        private FileHierarchy _fileHierarchy;
        private SentroRequest _request;
        private SentroResponse _response;
        private bool _tempCreated;

        public ConnectionBuffer(SentroRequest request)
        {
            _fileHierarchy = FileHierarchy.GetInstance();
            _request = request;
            _response = new SentroResponse();
        }

        public void Buffer(ref byte[] bytes, int length)
        {
            if (!_response.CanHoldMore(length))
            {
                if (!_tempCreated)
                    Flush(_request);

                Flush(_response);
                _tempCreated = true;
            }

            _response.Push(ref bytes,length);
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

        private void Flush(ITcpStreem streem)
        {
            throw new NotImplementedException();
        }
    }
}
