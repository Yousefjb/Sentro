using System;

namespace Sentro.Traffic
{
    public class HttpResponseHeaders
    {
        public int ContentLength { get; set; }
        public string ContentType { get; set; }
        public int StatusCode { get; set; }
        public DateTime Expires { get; set; }
        public string CacheControl { get; set; }
        public string Etag { get; set; }
        public DateTime LastModified { get; set; }
    }
}
