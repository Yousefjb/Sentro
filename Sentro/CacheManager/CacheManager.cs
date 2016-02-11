using System;
using System.Net; // new
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Sentro.CacheManager
{
    class CacheManager
    {
        public CacheManager()
        {

        }
        /// <summary>
        /// Function to normalize a url 
        /// </summary>
        /// <param name="url">the url to normalize</param>
        /// <returns>Normalized Url</returns>
        public string normalize(string url)
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

            // print the normalized url
            //Console.WriteLine("\nNormalized URL: " + url);
            

            

            return url;
        }

        /// <summary>
        /// Converts a normalized URL to hashed key by MurMur2 Algorithm
        /// </summary>
        /// <param name="url">Normalized URL</param>
        /// <returns>hashed key of url as string</returns>
        public string hash(string url)
        {
            Murmur2 _mm = new Murmur2();
            var byte_array = Encoding.ASCII.GetBytes(url);
            var hashed_url = _mm.Hash(byte_array);
            
            // print the new url
            Console.WriteLine("\nHashed URL: " + hashed_url.ToString("X"));


            return hashed_url.ToString("X");
        } 

        public int Hier(string _MainDirectory = "C:/Sentro/CacheStorage")
        {
            // Create Main folder if not exist
            if (!Directory.Exists(_MainDirectory))
                Directory.CreateDirectory(_MainDirectory);
            string level1path, level2path;
            // Create first level folders
            for (int i=0; i<16; i++)
            {
                level1path = _MainDirectory + "/" + i.ToString("X");
                if( !Directory.Exists(level1path) )
                    Directory.CreateDirectory(level1path);
                // Create second level folders
                for (int k = 0; k < 256; k++)
                {
                    level2path = level1path + "/" + k.ToString("X2");
                    if (!Directory.Exists(level2path))
                        Directory.CreateDirectory(level2path);
                }
                Console.WriteLine(level1path + " ..Created");

            }



            return 0;
        }
    }

    #region Hashing Algorithm

    public interface IHashAlgorithm
    {
        UInt32 Hash(Byte[] data);
    }
    public interface ISeededHashAlgorithm : IHashAlgorithm
    {
        UInt32 Hash(Byte[] data, UInt32 seed);
    }

    public class SuperFastHashSimple : IHashAlgorithm
    {
        public UInt32 Hash(Byte[] dataToHash)
        {
            Int32 dataLength = dataToHash.Length;
            if (dataLength == 0)
                return 0;
            UInt32 hash = Convert.ToUInt32(dataLength);
            Int32 remainingBytes = dataLength & 3; // mod 4
            Int32 numberOfLoops = dataLength >> 2; // div 4
            Int32 currentIndex = 0;
            while (numberOfLoops > 0)
            {
                hash += BitConverter.ToUInt16(dataToHash, currentIndex);
                UInt32 tmp = (UInt32)(BitConverter.ToUInt16(dataToHash, currentIndex + 2) << 11) ^ hash;
                hash = (hash << 16) ^ tmp;
                hash += hash >> 11;
                currentIndex += 4;
                numberOfLoops--;
            }

            switch (remainingBytes)
            {
                case 3:
                    hash += BitConverter.ToUInt16(dataToHash, currentIndex);
                    hash ^= hash << 16;
                    hash ^= ((UInt32)dataToHash[currentIndex + 2]) << 18;
                    hash += hash >> 11;
                    break;
                case 2:
                    hash += BitConverter.ToUInt16(dataToHash, currentIndex);
                    hash ^= hash << 11;
                    hash += hash >> 17;
                    break;
                case 1:
                    hash += dataToHash[currentIndex];
                    hash ^= hash << 10;
                    hash += hash >> 1;
                    break;
                default:
                    break;
            }

            /* Force "avalanching" of final 127 bits */
            hash ^= hash << 3;
            hash += hash >> 5;
            hash ^= hash << 4;
            hash += hash >> 17;
            hash ^= hash << 25;
            hash += hash >> 6;

            return hash;
        }
    }
    class Murmur2 : ISeededHashAlgorithm
    {
        public UInt32 Hash(Byte[] data)
        {
            return Hash(data, 0xc58f1a7b);
        }
        const UInt32 m = 0x5bd1e995;
        const Int32 r = 24;

        public unsafe uint Hash(byte[] data, uint seed)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0;
            UInt32 h = seed ^ (UInt32)length;
            Int32 remainingBytes = length & 3; // mod 4
            Int32 numberOfLoops = length >> 2; // div 4
            fixed (byte* firstByte = &(data[0]))
            {
                UInt32* realData = (UInt32*)firstByte;
                while (numberOfLoops != 0)
                {
                    UInt32 k = *realData;
                    k *= m;
                    k ^= k >> r;
                    k *= m;

                    h *= m;
                    h ^= k;
                    numberOfLoops--;
                    realData++;
                }
                switch (remainingBytes)
                {
                    case 3:
                        h ^= (UInt16)(*realData);
                        h ^= ((UInt32)(*(((Byte*)(realData)) + 2))) << 16;
                        h *= m;
                        break;
                    case 2:
                        h ^= (UInt16)(*realData);
                        h *= m;
                        break;
                    case 1:
                        h ^= *((Byte*)realData);
                        h *= m;
                        break;
                    default:
                        break;
                }
            }

            // Do a few final mixes of the hash to ensure the last few
            // bytes are well-incorporated.

            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;

            return h;
        }
    }
    #endregion


}
