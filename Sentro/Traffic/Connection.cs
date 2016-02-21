namespace Sentro.Traffic
{
    class Connection
    {
        public const string Tag = "Connection";
        public string SourceIp { get; }        
        public ushort SourcePort { get; }        
        public string DestinationIp { get; }        
        public ushort DestinationPort { get; }

        public Connection(string sourceIp, ushort sourcePort, string destinationIp, ushort destinationPort)
        {
            SourceIp = sourceIp;            
            DestinationIp = destinationIp;
            SourcePort = sourcePort;
            DestinationPort = destinationPort;
        }


        /// <summary>
        /// Overrided in order to catch both sides of the connection 
        /// with the same connection object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (!(obj is Connection))
                return false;
            Connection con = (Connection)obj;

            bool result = ((con.SourceIp.Equals(SourceIp)) && (con.SourcePort == SourcePort) && (con.DestinationIp.Equals(DestinationIp)) && (con.DestinationPort == DestinationPort)) ||
                ((con.SourceIp.Equals(DestinationIp)) && (con.SourcePort == DestinationPort) && (con.DestinationIp.Equals(SourceIp)) && (con.DestinationPort == SourcePort));

            return result;
        }

        public override int GetHashCode()
        {
            return ((SourceIp.GetHashCode() ^ SourcePort.GetHashCode()) as object).GetHashCode() ^
                ((DestinationIp.GetHashCode() ^ DestinationPort.GetHashCode()) as object).GetHashCode();
        }       
    }

}
