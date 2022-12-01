using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Networks;

#pragma warning restore 1998
[UsedImplicitly]
public class UpdateMachineNetworksCommandHandler : IHandleMessages<OperationTask<UpdateMachineNetworksCommand>>
{
    private readonly IBus _bus;
    private readonly IStateStore _stateStore;
    private readonly ICatletIpManager _ipManager;

    public UpdateMachineNetworksCommandHandler(IBus bus, ICatletIpManager ipManager, IStateStore stateStore)
    {
        _bus = bus;
        _ipManager = ipManager;
        _stateStore = stateStore;
    }

    public async Task Handle(OperationTask<UpdateMachineNetworksCommand> message)
    {
        await message.Command.Config.Networks.Map(cfg =>
                
                from network in _stateStore.ReadBySpecAsync<VirtualNetwork, VirtualNetworkSpecs.GetByName>(
                    new VirtualNetworkSpecs.GetByName(message.Command.ProjectId, cfg.Name)
                    ,Error.New($"Network '{cfg.Name}' not found in project {message.Command.ProjectId}"))

                from networkPort in GetOrAddAdapterPort(
                        network, message.Command.MachineId, cfg.AdapterName, CancellationToken.None)

                let fixedMacAddress =
                    message.Command.Config.VM.NetworkAdapters.Find(x => x.Name == cfg.AdapterName)
                        .Map(x => x.MacAddress)
                        .IfNone("")
                let _ = UpdatePort(networkPort, cfg.AdapterName, fixedMacAddress)

                let cancelToken = new CancellationTokenSource()
                from ips in _ipManager.ConfigurePortIps(
                    message.Command.ProjectId, 
                    networkPort, 
                    message.Command.Config.Networks, 
                    cancelToken.Token)

                select new MachineNetworkSettings
                {
                    NetworkProviderName = network.NetworkProvider,
                    NetworkName = cfg.Name,
                    AdapterName = cfg.AdapterName,
                    PortName = networkPort.Name,
                    MacAddress = networkPort.MacAddress,
                    AddressesV4 = string.Join(',', ips.Where(x=>x.AddressFamily == AddressFamily.InterNetwork)),
                    AddressesV6 = string.Join(',', ips.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6))

                }

            )
            .TraverseParallel(l => l)
            .Map(settings => new UpdateMachineNetworksCommandResponse { NetworkSettings = settings.ToArray() })
            .MatchAsync(
                LeftAsync: l => _bus.SendLocal(
                    OperationTaskStatusEvent.Failed(
                        message.OperationId, message.TaskId, l.Message)),
                RightAsync: map => _bus.SendLocal(
                    OperationTaskStatusEvent.Completed(
                        message.OperationId, message.TaskId, map))

            );

    }

    private EitherAsync<Error, CatletNetworkPort> GetOrAddAdapterPort(VirtualNetwork network, Guid catletId, string adapterName, 
        CancellationToken cancellationToken)
    {
        var portName = $"{catletId}_{adapterName}";

        return _stateStore.For<VirtualNetworkPort>()
            .IO.GetBySpecAsync(new NetworkPortSpecs.GetByNetworkAndName(network.Id, portName), cancellationToken)
            .MapAsync(async option => await option.IfNoneAsync(() =>
                {
                    var port = new CatletNetworkPort
                    {
                        Id = Guid.NewGuid(),
                        CatletId = catletId,
                        Name = portName,
                        NetworkId = network.Id

                    };
                    return _stateStore.For<VirtualNetworkPort>().AddAsync(port, cancellationToken);
                }
            ).ConfigureAwait(false))
            .Map(p => (CatletNetworkPort)p);

    }


    private static string GenerateMacAddress(Guid valueId, string adapterName)
    {
        var id = $"{valueId}_{adapterName}";
        var crc = new Crc32();

        string result = null;

        var arrayData = Encoding.ASCII.GetBytes(id);
        var arrayResult = crc.ComputeHash(arrayData);
        foreach (var t in arrayResult)
        {
            var temp = Convert.ToString(t, 16);
            if (temp.Length == 1)
                temp = $"0{temp}";
            result += temp;
        }

        return "d2ab" + result;
    }


    private static Unit UpdatePort(CatletNetworkPort networkPort, string adapterName, string fixedMacAddress)
    {
        if (!string.IsNullOrEmpty(fixedMacAddress))
            networkPort.MacAddress = fixedMacAddress;
        else
        {
            networkPort.MacAddress ??= GenerateMacAddress(networkPort.CatletId.GetValueOrDefault(), adapterName);
        }

        return Unit.Default;
    }
}