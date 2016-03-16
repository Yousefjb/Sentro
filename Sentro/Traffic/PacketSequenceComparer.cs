using System.Collections.Generic;
namespace Sentro.Traffic
{
    public class PacketSequenceComparer : IComparer<uint>
    {
        public int Compare(uint x, uint y)
        {
            if (x > y)
                return 1;
            if (y < x)
                return -1;
            return 0;
        }
    }
}
