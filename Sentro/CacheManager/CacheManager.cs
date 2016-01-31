using System;
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
            // *************************************
            //              Stage One
            // *************************************

            //
            // 1. Converting the scheme and host to lower case
            // 2. Capitalizing letters in escape sequences

            char[] url_char_array = url.ToCharArray(); // convert string to array of charachters


            for (int i = 0; i < url_char_array.Length; i++)
            {
                char x = url_char_array[i];
                if (x == '%')
                {
                    x = url_char_array[++i];
                    if (Char.IsLetter(x) && Char.IsLower(x))
                        url_char_array[i] = Char.ToUpper(x); // replace the char
                }
                else if (Char.IsLetter(x) && Char.IsUpper(x))
                    url_char_array[i] = Char.ToLower(x); // replace the char

            }


            //
            // print the new url
            Console.WriteLine(
                "input: " + url +
                "\noutput: " + new string(url_char_array)
                );
            return 0; // 0 = no error ,else error code number
        }
    }
}
