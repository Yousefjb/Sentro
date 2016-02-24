using System;
using System.Text;
using Sentro.Utilities;

namespace Sentro.Cache
{
    class Normalizer
    {
        public static string Tag = "Normalizer";
        private static Normalizer _normalizer;
        private static FileLogger _fileLogger;

        private Normalizer()
        {            
            _fileLogger = FileLogger.GetInstance();
        }

        public static Normalizer GetInstance()
        {
            return _normalizer ?? (_normalizer = new Normalizer());
        }

        public string Normalize(string url)
        {
            try
            {
                url = new UriBuilder(url).Uri.ToString();

                var i = url.IndexOf("#", StringComparison.Ordinal);
                if (i != -1)
                    url = url.Remove(i);

                url = url.Replace("//", "/");

                url = url.Replace("www.", "");

                // Sorting query parameters
                var queryString = url.Substring(url.IndexOf('?') + 1).Split('&');
                Array.Sort(queryString);

                var builder = new StringBuilder();
                builder.Append(url.Substring(0, url.IndexOf('?') + 1));

                foreach (var value in queryString)
                {
                    builder.Append(value);
                    builder.Append('&');
                }
                builder.Remove(builder.Length - 1, 1);
                url = builder.ToString();
                return url;
            }
            catch (Exception e)
            {
                _fileLogger.Error(Tag,e.ToString());
                return url;
            }            
        }
    }
}
