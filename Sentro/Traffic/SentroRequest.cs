using System;
using System.Text;
using System.Text.RegularExpressions;
using Sentro.Cache;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    internal class SentroRequest : TcpStreem
    {
        public const string Tag = "SentroHttpRequest";   
        private string _requestUri = "";


        public SentroRequest(byte[] bytes, int length)
            : base(bytes, length, (int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.OutBufferSize))
        {

        }

        public SentroRequest() : base((int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.OutBufferSize))
        {

        }

        public string RequestUri()
        {
            if (_requestUri.Length != 0)
                return _requestUri;

            var ascii = Encoding.ASCII.GetString(Buffer[0]);
            var result = Regex.Match(ascii, CommonRegex.HttpGetUriMatch);
            string path = result.Value;
            string host = result.NextMatch().Value;
            _requestUri = host + path;
            return _requestUri;
        }

        public string RequestUriHashed()
        {
            var normalized = new Normalizer().Normalize(RequestUri());
            return new Murmur2().Hash(Encoding.ASCII.GetBytes(normalized));
        }

    }
}
