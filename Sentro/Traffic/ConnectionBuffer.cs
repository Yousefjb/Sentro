using System;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    class ConnectionBuffer
    {
        public const string Tag = "ConnectionBuffer";
        private FileHierarchy _fileHierarchy;
        private ITcpStreem _request,_response;

        public ConnectionBuffer(SentroRequest request)
        {
            _fileHierarchy = FileHierarchy.GetInstance();
            _request = request;
            _response = new SentroResponse();
        }

        public void Buffer(ref byte[] bytes, int length)
        {
            throw new NotImplementedException();
        }

        public SentroRequest Request()
        {
            throw new NotImplementedException();
        }

        public SentroResponse Response()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
