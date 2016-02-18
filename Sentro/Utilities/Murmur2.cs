using System;

namespace Sentro.Utilities
{

    public interface IHashAlgorithm
    {
        string Hash(Byte[] data);
    }

    public interface ISeededHashAlgorithm : IHashAlgorithm
    {
        string Hash(Byte[] data, UInt32 seed);
    }

    public class SuperFastHashSimple : IHashAlgorithm
    {
        public const string Tag = "SuperFastHashSimple";
        public string Hash(Byte[] dataToHash)
        {
            Int32 dataLength = dataToHash.Length;
            if (dataLength == 0)
                return 0.ToString("X8");
            UInt32 hash = Convert.ToUInt32(dataLength);
            Int32 remainingBytes = dataLength & 3; // mod 4
            Int32 numberOfLoops = dataLength >> 2; // div 4
            Int32 currentIndex = 0;
            while (numberOfLoops > 0)
            {
                hash += BitConverter.ToUInt16(dataToHash, currentIndex);
                UInt32 tmp = (UInt32) (BitConverter.ToUInt16(dataToHash, currentIndex + 2) << 11) ^ hash;
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
                    hash ^= ((UInt32) dataToHash[currentIndex + 2]) << 18;
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

            return hash.ToString("X8");
        }
    }

    internal class Murmur2 : ISeededHashAlgorithm
    {
        public const string Tag = "Murmur2";
        public string Hash(Byte[] data)
        {
            return Hash(data, 0xc58f1a7b);
        }

        private const UInt32 m = 0x5bd1e995;
        private const Int32 r = 24;

        public unsafe string Hash(byte[] data, uint seed)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0.ToString("X8");
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
                    default:
                        break;
                }
            }

            // Do a few final mixes of the hash to ensure the last few
            // bytes are well-incorporated.

            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;
            
            return h.ToString("X8");
        }
    }

}
