
namespace Sentro.Utilities
{

    /*
        Responsipility : Group up all common regex used by sentro
    */

    public sealed class CommonRegex
    {
        public const string Tag = "CommonRegex";

        public const string Ip = @"((?:(?:1\d?\d|[1-9]?\d|2[0-4]\d|25[0-5])\.){3}(?:1\d?\d|[1-9]?\d|2[0-4]\d|25[0-5]))";
        public const string HttpGet = @"^GET .+ HTTP\/1.[0-1]";
        public const string HttpResonse = @"^HTTP\/1.[0-1] \d{3}";
        public const string HttpGetUriMatch = @"^GET (.+) HTTP\/1.[0-1](?:.+\n)+Host: (.+)";
    }
}