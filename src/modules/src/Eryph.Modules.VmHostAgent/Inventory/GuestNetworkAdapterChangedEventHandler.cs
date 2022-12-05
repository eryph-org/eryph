using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
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
                    (from hostInfo in _hostInfoProvider.GetHostInfoAsync()
                    let adapter = a.Cast<VMNetworkAdapter>()
                    select 
                        new VirtualMachineNetworkChangedEvent
                        {
                            VMId = message.VmId,
                            ChangedAdapter = new VirtualMachineNetworkAdapterData
                            {
                                Id = a.Value.Id,
                                AdapterName = a.Value.Name,
                                VLanId = (ushort) a.GetProperty(x=>x.VlanSetting)
                                    .Cast<VMNetworkAdapterVlanSetting>().Value.AccessVlanId,
                                VirtualSwitchName = adapter.Value.SwitchName,
                                VirtualSwitchId = adapter.Value.SwitchId,
                                MacAddress = a.Value.MacAddress
                            },
                            ChangedNetwork = VirtualNetworkQuery.GetNetworksByAdapters(hostInfo, new []{adapter.Value})
                                .FirstOrDefault()
                        }).ToEither()).ToAsync().MatchAsync(
                    r => _bus.Publish(r),
                    l => { _log.LogError(l.Message); });
        }


        private static Either<Error, TypedPsObject<T>> SingleOrFailure<T>(Seq<TypedPsObject<T>> sequence)
        {
            return sequence.HeadOrNone().ToEither(Errors.SequenceEmpty);
        }

        private Task<Either<Error, Seq<TypedPsObject<T>>>> GetVMs<T>(Guid vmId)
        {
            return _engine.GetObjectsAsync<T>(new PsCommandBuilder()
                .AddCommand("Get-VM").AddParameter("Id", vmId)).ToError();
        }
    }
}