using System;
using System.Text;

namespace Sentro.CacheManager
{
    class Normalizer
    {
        public static string Tag = "Normalizer";
        private static Normalizer _normalizer;

        private Normalizer()
        {
        }

        public static Normalizer GetInstance()
        {
            return _normalizer ?? (_normalizer = new Normalizer());
        }

        /// <summary>
        /// Function to normalize a url 
        /// </summary>
        /// <param name="url">the url to normalize</param>
        /// <returns>Normalized Url</returns>
        public string Normalize(string url)
        {
            
            url = new UriBuilder(url).Uri.ToString();

            // Removing directory index (index pages)
            url = url.Replace("Default.asp", "");            
            url = url.Replace("index.php", "");
            url = url.Replace("index.html", "");
            url = url.Replace("index.htm", "");
            url = url.Replace("index.shtml", "");
            url = url.Replace("default.htm", "");
            url = url.Replace("default.html", "");
            url = url.Replace("home.html", "");
            url = url.Replace("home.htm", "");
            url = url.Replace("Index.html", "");
            url = url.Replace("Index.htm", "");
            url = url.Replace("Index.php", "");

            int i = -1;
            i = url.IndexOf("#");
            if (i != -1)
                url = url.Remove(i);
            
            url = url.Replace("//", "/");
           
            url = url.Replace("www.", "");

            // Sorting query parameters
            string[] queryString = url.Substring(url.IndexOf('?') + 1).Split('&');
            Array.Sort(queryString);

            StringBuilder builder = new StringBuilder();
            builder.Append(url.Substring(0, url.IndexOf('?') + 1));

            foreach (string value in queryString)
            {
                builder.Append(value);
                builder.Append('&');
            }
            builder.Remove(builder.Length - 1, 1);
            url = builder.ToString();

            return url;
        }
    }
}
