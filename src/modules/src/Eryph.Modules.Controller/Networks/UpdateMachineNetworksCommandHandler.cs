using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

namespace Eryph.Modules.Controller.Networks;

#pragma warning restore 1998
[UsedImplicitly]
public class UpdateMachineNetworksCommandHandler : IHandleMessages<OperationTask<UpdateMachineNetworksCommand>>
{
    private readonly IBus _bus;
    private readonly IStateStore _stateStore;
    private readonly ICatletIpManager _ipManager;

    public UpdateMachineNetworksCommandHandler(IBus bus, ICatletIpManager ipManager, 
        IStateStore stateStore)
    {
        _bus = bus;
        _ipManager = ipManager;
        _stateStore = stateStore;
    }

    public async Task Handle(OperationTask<UpdateMachineNetworksCommand> message)
    {
        await ProgressMessage(message.OperationId, message.TaskId, "Updating Catlet network settings");

        await message.Command.Config.Networks.Map(cfg =>
                
                from network in _stateStore.ReadBySpecAsync<VirtualNetwork, VirtualNetworkSpecs.GetByName>(
                    new VirtualNetworkSpecs.GetByName(message.Command.ProjectId, cfg.Name)
                    ,Error.New($"Network '{cfg.Name}' not found in project {message.Command.ProjectId}"))

                let c1 = new CancellationTokenSource(5000)

                from networkPort in GetOrAddAdapterPort(
                        network, message.Command.MachineId, cfg.AdapterName, c1.Token)

                let c2 = new CancellationTokenSource()

                from floatingPort in GetOrAddFloatingPort(
                        networkPort, Option<string>.None,
                        "default", "default",
                        "default", c2.Token).ToAsync()

                let fixedMacAddress =
                    message.Command.Config.VCatlet.NetworkAdapters.Find(x => x.Name == cfg.AdapterName)
                        .Map(x => x.MacAddress)
                        .IfNone("")
                let _ = UpdatePort(networkPort, cfg.AdapterName, fixedMacAddress)

                let c3 = new CancellationTokenSource()
                from ips in _ipManager.ConfigurePortIps(
                    message.Command.ProjectId, 
                    networkPort, 
                    message.Command.Config.Networks, 
                    c3.Token)
                
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
                LeftAsync: l =>
                {
                    return _bus.SendLocal(
                        OperationTaskStatusEvent.Failed(
                            message.OperationId, message.TaskId, l.Message));
                },
                RightAsync: map => _bus.SendLocal(
                    OperationTaskStatusEvent.Completed(
                        message.OperationId, message.TaskId, map))

            );

    }

    private async Task ProgressMessage(Guid operationId, Guid taskId, string message)
    {
        using var scope = new RebusTransactionScope();
        await _bus.Publish(new OperationTaskProgressEvent
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            TaskId = taskId,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        // commit it like this
        await scope.CompleteAsync().ConfigureAwait(false);
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
                        NetworkId = network.Id,
                        IpAssignments = new List<IpAssignment>()

                    };
                    return _stateStore.For<VirtualNetworkPort>().AddAsync(port, cancellationToken);
                }
            ).ConfigureAwait(false))
            .Map(p => (CatletNetworkPort)p);

    }

    private async Task<Either<Error, FloatingNetworkPort>> GetOrAddFloatingPort(CatletNetworkPort adapterPort,
        Option<string> portName, string providerName, string providerSubnetName, string providerPoolName,
        CancellationToken cancellationToken)

    {
        await _stateStore.LoadPropertyAsync(adapterPort, x => x.FloatingPort, cancellationToken);

        if (adapterPort.FloatingPort != null)
        {
            var floatingPort = adapterPort.FloatingPort;
            if (floatingPort.ProviderName != providerName || floatingPort.SubnetName !=
                providerSubnetName || floatingPort.PoolName != providerPoolName)
            {
                adapterPort.FloatingPort = null;

                if (portName.IsNone) // not a named port, then remove
                    await _stateStore.For<FloatingNetworkPort>().DeleteAsync(floatingPort, cancellationToken);
            }

        }

        if (adapterPort.FloatingPort != null)
            return adapterPort.FloatingPort;

        var port = new FloatingNetworkPort
        {
            Id = Guid.NewGuid(),
            Name = portName.IfNone(adapterPort.Name),
            ProviderName = providerName,
            SubnetName = providerSubnetName,
            PoolName = providerPoolName,
            MacAddress = FormatMacAddress(GenerateMacAddress(Guid.NewGuid(), ""))
        };

        adapterPort.FloatingPort = port;

        return await _stateStore.For<FloatingNetworkPort>().AddAsync(port, cancellationToken);

    }

    private static string FormatMacAddress(string input)
    {
        const string regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
        const string replace = "$1:$2:$3:$4:$5:$6";

        return Regex.Replace(input, regex, replace).ToLowerInvariant();

    }

    private static string GenerateMacAddress(Guid valueId, string adapterName)
    {
        var id = $"{valueId}_{adapterName}";
        var crc = new Crc32();

        string? result = null;

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
            networkPort.MacAddress = FormatMacAddress(fixedMacAddress);
        else
        {
            networkPort.MacAddress ??= FormatMacAddress(GenerateMacAddress(networkPort.CatletId.GetValueOrDefault(), adapterName));
        }

        return Unit.Default;
    }
}