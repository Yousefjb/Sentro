using System;
using System.Net; // new
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentro.CacheManager
{
    class CacheManager
    {
        public CacheManager()
        {

        }
        public int normalize(string url)
        {
            
            UriBuilder u = new UriBuilder(url);
            url = u.Uri.ToString();
                        
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

            // Removing the fragment (#xxx)
            int i = -1;
            i = url.IndexOf("#");
            if (i != -1)
                url = url.Remove(i);

            // Removing duplicated slashes (//)
            url = url.Replace("//", "/");

            // Removing (www)
            url = url.Replace("www.", "");

            // Sorting query parameters
            string[] queryString = url.Substring(url.IndexOf('?')+1).Split('&');
            Array.Sort(queryString);

            StringBuilder builder = new StringBuilder();
            builder.Append(url.Substring(0, url.IndexOf('?')+1));

            foreach ( string value in queryString)
            {
                builder.Append(value);
                builder.Append('&');
            }
            builder.Remove(builder.Length - 1,1);
            url = builder.ToString();

            
            // print the new url
            Console.WriteLine("\noutput: " + url );
           
            
            return 0; // 0 = no error ,else error code number
        }
    }
}
