using System;
using System.Web; // new
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
            /*
            UriBuilder u = new UriBuilder(url);
            Console.WriteLine(u.Uri.ToString());
            */
            
            //
            // Stage One
            // 1. Converting the scheme and host to lower case
            url = url.ToLower();

            // 2. Capitalizing letters in escape sequences
            char[] url_char_array = url.ToCharArray(); // convert string to array of charachters
            for (int i = 0; i < url_char_array.Length; i++)
            {
                char x = url_char_array[i];
                if (x == '%')
                {
                    url_char_array[++i] = Char.ToUpper(x); // replace the char
                    url_char_array[++i] = Char.ToUpper(x); 
                }
            }
            url = new string(url_char_array);

            // 3. Decoding percent-encoded octets of unreserved characters
            url = System.Net.WebUtility.UrlDecode(url);


            

            // print the new url
            Console.WriteLine(
                "input: " + url +
                "\noutput: " + url );
           
            
            return 0; // 0 = no error ,else error code number
        }
    }
}
