using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    internal class GuestNetworkAdapterChangedEventHandler : IHandleMessages<GuestNetworkAdapterChangedEvent>
    {
        private readonly IBus _bus;
        private readonly IPowershellEngine _engine;
        private readonly ILogger _log;
        private readonly IHostInfoProvider _hostInfoProvider;

        public GuestNetworkAdapterChangedEventHandler(IBus bus, IPowershellEngine engine, ILogger log, IHostInfoProvider hostInfoProvider)
        {
            _bus = bus;
            _engine = engine;
            _log = log;
            _hostInfoProvider = hostInfoProvider;
        }

        public Task Handle(GuestNetworkAdapterChangedEvent message)
        {
            var findAdapterForMessage = Prelude.fun(
                (Seq<TypedPsObject<VMNetworkAdapter>> seq) => NetworkAdapterQuery.FindAdapter(seq, message.AdapterId));

            var getNetworkAdapters = Prelude.fun(
                (TypedPsObject<object> vm) => NetworkAdapterQuery.GetNetworkAdapters(vm, _engine));

            return GetVMs<object>(message.VmId)
                .BindAsync(SingleOrFailure)
                .BindAsync(getNetworkAdapters)
                .BindAsync(findAdapterForMessage)
                .BindAsync(a => 
                    from hostInfo in _hostInfoProvider.GetHostInfoAsync()
                    let hostNetwork = hostInfo.VirtualNetworks.FirstOrDefault(x=>x.VirtualSwitchId == a.Value.SwitchId)
                    select 
                        new VirtualMachineNetworkChangedEvent
                        {
                            VMId = message.VmId,
                            ChangedAdapter = new VirtualMachineNetworkAdapterData
                            {
                                Id = a.Value.Id,
                                AdapterName = a.Value.Name,
                                VLanId = (ushort) a.Value.VlanSetting.AccessVlanId,
                                VirtualSwitchName = a.Value.SwitchName,
                                MACAddress = a.Value.MacAddress
                            },
                            ChangedNetwork = new MachineNetworkData
                            {
                                Name = hostNetwork?.Name,
                                IPAddresses = message.IPAddresses,
                                Subnets = NetworkAddresses
                                    .AddressesAndSubnetsToSubnets(message.IPAddresses, message.Netmasks).ToArray(),
                                DefaultGateways = message.DefaultGateways,
                                DnsServers = message.DnsServers,
                                DhcpEnabled = message.DhcpEnabled
                            }
                        }).ToAsync().MatchAsync(
                    r => _bus.Publish(r),
                    l => { _log.LogError(l.Message); });
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