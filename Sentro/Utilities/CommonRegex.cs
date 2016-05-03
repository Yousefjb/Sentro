namespace Sentro.Utilities
{
    /*
        Responsipility : Group up all common regex used by sentro
    */
    public sealed class CommonRegex
    {
        public const string Tag = "CommonRegex";

        public const string Ip = @"((?:(?:1\d?\d|[1-9]?\d|2[0-4]\d|25[0-5])\.){3}(?:1\d?\d|[1-9]?\d|2[0-4]\d|25[0-5]))";                
        public const string HttpGetUriMatch = @"^GET (?<path>.+) HTTP\/1.[0-1](?:.+\n)+Host: (?<host>.+)";
        public const string HttpContentLengthMatch = @"^content-length: (\d+)";
        public const string HttpContentTypeMatch = @"content-type: (.*)\r\n";
        public const string HttpStatusCodeMatch = @"HTTP\/1.[0-1] (?<code>\d{3})";
    }
}