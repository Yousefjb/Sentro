using System;
using System.Text;

namespace Sentro.Utilities
{
    /*
        Responsibility : Implementaion of Murmur2 algorithm
    */
    public class Murmur2
    {
        public const string Tag = "Murmur2";
        public static uint Hash(Byte[] data)
        {
            return Hash(data, 0xc58f1a7b);
        }

        public static string HashX8(Byte[] data)
        {
            return Hash(data).ToString("X8");
        }

        public static uint Hash(string data,Encoding encoding)
        {
            return Hash(encoding.GetBytes(data));
        }

        public static string HashX8(string data, Encoding encoding)
        {
            return Hash(data, encoding).ToString("X8");
        }

        private const UInt32 m = 0x5bd1e995;
        private const Int32 r = 24;

        public static unsafe uint Hash(byte[] data, uint seed)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0;
            UInt32 h = seed ^ (UInt32) length;
            Int32 remainingBytes = length & 3; // mod 4
            Int32 numberOfLoops = length >> 2; // div 4
            fixed (byte* firstByte = &(data[0]))
            {
                UInt32* realData = (UInt32*) firstByte;
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
                        h ^= (UInt16) (*realData);
                        h ^= ((UInt32) (*(((Byte*) (realData)) + 2))) << 16;
                        h *= m;
                        break;
                    case 2:
                        h ^= (UInt16) (*realData);
                        h *= m;
                        break;
                    case 1:
                        h ^= *((Byte*) realData);
                        h *= m;
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

}
