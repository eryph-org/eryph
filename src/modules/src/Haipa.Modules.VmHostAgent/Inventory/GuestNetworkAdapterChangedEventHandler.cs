using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.VmManagement;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Networking;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
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
            var findAdapterForMessage = Prelude.fun(
                (Seq<TypedPsObject<VMNetworkAdapter>> seq) => NetworkAdapterQuery.FindAdapter(seq, message.AdapterId));

            var getNetworkAdapters = Prelude.fun(
                (TypedPsObject<object> vm) => NetworkAdapterQuery.GetNetworkAdapters(vm, _engine));

            var networkNames = new List<string>();

            return GetVMs<object>(message.VmId)
                .BindAsync(SingleOrFailure)
                .BindAsync(getNetworkAdapters)
                .BindAsync(findAdapterForMessage)
                .ToAsync().MatchAsync(
                    RightAsync: (a => _bus.Publish(
                        new VirtualMachineNetworkChangedEvent
                        {
                            VMId = message.VmId,
                            ChangedAdapter = new VirtualMachineNetworkAdapterInfo()
                            {
                                Id = a.Value.Id,
                                AdapterName = a.Value.Name,
                                VLanId = (ushort) a.Value.VlanSetting.AccessVlanId,
                                VirtualSwitchName = a.Value.SwitchName,
                                MACAddress = a.Value.MacAddress
                            },
                            ChangedNetwork = new MachineNetworkInfo
                            {
                                Name = Networks.GenerateName(ref networkNames, a.Value),
                                IPAddresses = message.IPAddresses,
                                Subnets = NetworkAddresses.AddressesAndSubnetsToSubnets(message.IPAddresses,message.Netmasks).ToArray(),
                                DefaultGateways = message.DefaultGateways,
                                DnsServers = message.DnsServers,
                                DhcpEnabled = message.DhcpEnabled,
                            }
                        })),
                    Left: l =>
                    {

                    });
        }


        private static Either<PowershellFailure, TypedPsObject<T>> SingleOrFailure<T>(Seq<TypedPsObject<T>> sequence)
        {
            return sequence.HeadOrNone().ToEither(new PowershellFailure());
        }

        private Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetVMs<T>(Guid vmId)
        {
            return _engine.GetObjectsAsync<T>(new PsCommandBuilder()
                .AddCommand("Get-VM").AddParameter("Id", vmId));
        }
    }
}