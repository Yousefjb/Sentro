using System;
using System.Collections.Generic;
using Sentro.Utilities;

namespace Sentro.Traffic
{
    internal class SentroResponse  : TcpStreem
    {
        public new const string Tag = "SentroResponse";

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

        public static SentroResponse CreateFromBytes(byte[] bytes, int length)
        {
            var response = new SentroResponse();
            response.LoadFrom(bytes,length);         
            return response;
        }

        public void SetAddressesFrom(SentroRequest request)
        {                      
            SetDestinationIp(request.DestinationIp());
            SetDestinationPort(request.DestinationPort());
            SetSourceIp(request.SourceIp());
            SetSourcePort(request.SourcePort());
        }
    }
}
