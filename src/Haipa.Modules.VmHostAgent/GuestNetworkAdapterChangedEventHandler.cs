using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Events;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    internal class GuestNetworkAdapterChangedEventHandler : IHandleMessages<GuestNetworkAdapterChangedEvent>
    {
        private readonly IBus _bus;
        private readonly IPowershellEngine _engine;

        public GuestNetworkAdapterChangedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
        }

        public Task Handle(GuestNetworkAdapterChangedEvent message)
        {
            Either<PowershellFailure, TypedPsObject<MinimizedVMNetworkAdapter>> FindAdapterForMessage(
                Seq<TypedPsObject<MinimizedVMNetworkAdapter>> seq) => FindAdapter(seq, message.AdapterId);

            return GetVMs(message.VmId)
                .BindAsync(SingleOrFailure)
                .BindAsync(GetNetworkAdapters)
                .BindAsync(FindAdapterForMessage)
                .ToAsync().MatchAsync(
                    RightAsync: (a => _bus.Publish(
                        new VirtualMachineNetworkChangedEvent
                        {
                            MachineId = message.VmId,
                            ChangedAdapter = new VirtualMachineNetworkAdapterInfo()
                            {
                                AdapterName = a.Value.Name,
                                VLanId = (ushort) a.Value.VlanSetting.AccessVlanId,
                                VirtualSwitchName = a.Value.SwitchName,
                                MACAddress = a.Value.MacAddress
                            },
                            ChangedNetwork = new VirtualMachineNetworkInfo
                            {
                                AdapterName = a.Value.Name,
                                IPAddresses = message.IPAddresses,
                                Subnets = AddressesAndSubnetsToSubnets(message.IPAddresses,message.Netmasks).ToArray(),
                                DefaultGateways = message.DefaultGateways,
                                DnsServers = message.DnsServers,
                                DhcpEnabled = message.DhcpEnabled,
                            }
                        })),
                    Left: l =>
                    {

                    });
        }

        private static IEnumerable<string> AddressesAndSubnetsToSubnets(IReadOnlyList<string> ipAddresses, IReadOnlyList<string> netmasks)
        {
            for (var i = 0; i < ipAddresses.Count; i++)
            {
                var address = ipAddresses[i];
                var netmask = netmasks[i];
                if (netmask.StartsWith("/"))
                {
                    yield return IPNetwork.Parse(address + netmask).ToString();
                }
                else
                    yield return IPNetwork.Parse(address,netmask).ToString();
            }
        }

        private Task<Either<PowershellFailure, Seq<TypedPsObject<MinimizedVMNetworkAdapter>>>> GetNetworkAdapters(TypedPsObject<MinimizedVirtualMachineInfo> vm)
        {
            return _engine.GetObjectsAsync<MinimizedVMNetworkAdapter>(
                new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter")
                    .AddParameter("VM", vm.PsObject));
        }

        private static Either<PowershellFailure, TypedPsObject<MinimizedVirtualMachineInfo>> SingleOrFailure(Seq<TypedPsObject<MinimizedVirtualMachineInfo>> sequence)
        {
            return sequence.HeadOrNone().ToEither(new PowershellFailure());
        }

        private static Either<PowershellFailure, TypedPsObject<MinimizedVMNetworkAdapter>> FindAdapter(Seq<TypedPsObject<MinimizedVMNetworkAdapter>> sequence, string adapterId)
        {
            adapterId = adapterId.Replace("Microsoft:GuestNetwork\\", "Microsoft:");
            return sequence.Find(a => a.Value.Id == adapterId)
                .ToEither(new PowershellFailure{Message = $"could not find network adapter with Id '{adapterId}'"});
        }

        private Task<Either<PowershellFailure, Seq<TypedPsObject<MinimizedVirtualMachineInfo>>>> GetVMs(Guid vmId)
        {
            return _engine.GetObjectsAsync<MinimizedVirtualMachineInfo>(new PsCommandBuilder()
                .AddCommand("Get-VM").AddParameter("Id", vmId));
        }

        private static string[] GetAddressesByFamily(IEnumerable<string> addresses, AddressFamily family)
        {
            return addresses.Where(a =>
            {
                var ipAddress = IPAddress.Parse(a);
                return ipAddress.AddressFamily == family;
            }).ToArray();
        }
    }
}