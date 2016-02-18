using Sentro.Utilities;

namespace Sentro.Traffic
{
    class ConnectionBuffer
    {
        public const string Tag = "ConnectionBuffer";
        private const int InMaxSize = 5242880; //5 MB
        private const int OutMaxSize = 2048; //2 KB
        private FileHierarchy _fileHierarchy;
        private ITcpStreem _request,_response;

        public ConnectionBuffer(ref byte[] buffer, uint length)
        {
            _fileHierarchy = FileHierarchy.GetInstance();
            _request = new SentroHttpRequest();
            _response = new SentroHttpResponse();
        } 
    }
}
