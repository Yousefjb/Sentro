using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sentro.Utilities
{
    /*
        Responsibility : Provide Parsing functionality with high perforamce with caching
    */
    static class HttpParser
    {
        private static readonly Dictionary<uint, ResponseHeader> Headers;

        public static int ContentLength(byte[] responseHeader, int length)
        {
            var hash = Murmur2.Hash(responseHeader);
            int contentLength;
            if (Headers.ContainsKey(hash))
                contentLength = Headers[hash].ContentLenght;
            else
            {
                var headers = Parse(responseHeader, length);
                Headers.Add(hash, headers);
                contentLength = headers.ContentLenght;
            }
            return contentLength;
        }

        static HttpParser()
        {
            Headers = new Dictionary<uint, ResponseHeader>();
        }

        private static ResponseHeader Parse(byte[] responseHeaders,int length)
        {
            //TODO: implement all needed headers + refactor
            var headers = new ResponseHeader();
            var ascii = Encoding.ASCII.GetString(responseHeaders);
            var result = Regex.Match(ascii, CommonRegex.HttpContentLengthMatch,
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            headers.ContentLenght = Convert.ToInt32(result.Groups[1].Value);

            return headers;
        }

        private class ResponseHeader
        {
            public int ContentLenght;
        }
    }
}
