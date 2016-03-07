using System;
using System.Collections.Generic;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    /*
        Responsibility : TcpStream that hold response bytes with http response specific functions
    */
    internal class SentroResponse  : TcpStream
    {
        public new const string Tag = "SentroResponse";
        
        private int _contentLength;
        private int _capturedLength;
        private FileLogger _fileLogger;    

        public SentroResponse(byte[] bytes, int length)
            : base(bytes, length, (int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.InBufferSize))
        {
            Init();
        }

        public SentroResponse() : base((int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.InBufferSize))
        {
            Init();
        }

        private void Init()
        {
            _fileLogger = FileLogger.GetInstance();            
        }

        public new void Push(byte[] bytes, int length)
        {
            base.Push(bytes,length);
            if (Buffer.Count == 1)
            {
                _contentLength = HttpParser.ContentLength(bytes, length);
                _fileLogger.Debug(Tag,"content legnth : " + _contentLength);               
            }
            else
            {
                _capturedLength += (length - Offset(bytes,(uint)length));
                _fileLogger.Debug(Tag,$"captured : {_capturedLength} contentlength : {_contentLength}");
                if (_capturedLength == _contentLength)
                    Complete = true;
            }
        }


        public List<byte[]> Packets() => Buffer;

        public bool Complete { get; private set; }

        public static SentroResponse CreateFromBytes(byte[] bytes, int length)
        {
            var response = new SentroResponse();
            response.LoadFrom(bytes,length);         
            return response;
        }

        public void SetAddressesFrom(SentroRequest request)
        {                      
            SetDestinationIp(request.DestinationIp());
            SetDestinationPort(request.DestinationPort());
            SetSourceIp(request.SourceIp());
            SetSourcePort(request.SourcePort());
        }
    }
}
