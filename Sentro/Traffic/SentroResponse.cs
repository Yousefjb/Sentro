using System;
using System.Collections.Generic;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    internal class SentroResponse  : TcpStreem
    {
        public const string Tag = "SentroHttpResponse";

        public SentroResponse(byte[] bytes, int length)
            : base(bytes, length, (int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.InBufferSize))
        {

        }

        public SentroResponse() : base((int) Convert.ToInt32(Settings.GetInstance().Setting.Traffic.InBufferSize))
        {

        }       

        public List<byte[]> Packets()
        {
            return Buffer;
        }
    }
}
