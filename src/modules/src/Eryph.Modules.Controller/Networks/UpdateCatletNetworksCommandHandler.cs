using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Networks;

#pragma warning restore 1998
[UsedImplicitly]
public class UpdateCatletNetworksCommandHandler : IHandleMessages<OperationTask<UpdateCatletNetworksCommand>>
{
    private readonly ITaskMessaging _messaging;
    private readonly IStateStore _stateStore;
    private readonly ICatletIpManager _ipManager;
    private readonly INetworkProviderManager _providerManager;


    public UpdateCatletNetworksCommandHandler(ITaskMessaging messaging, ICatletIpManager ipManager, 
        IStateStore stateStore, INetworkProviderManager providerManager)
    {
        _messaging = messaging;
        _ipManager = ipManager;
        _stateStore = stateStore;
        _providerManager = providerManager;
    }

    public async Task Handle(OperationTask<UpdateCatletNetworksCommand> message)
    {
        await _messaging.ProgressMessage(message, "Updating Catlet network settings");

        await message.Command.Config.Networks.Map(cfg =>

                from network in _stateStore.ReadBySpecAsync<VirtualNetwork, VirtualNetworkSpecs.GetByName>(
                    new VirtualNetworkSpecs.GetByName(message.Command.ProjectId, cfg.Name)
                    , Error.New($"Network '{cfg.Name}' not found in project {message.Command.ProjectId}"))

                from networkProviders in _providerManager.GetCurrentConfiguration()
                from networkProvider in networkProviders.NetworkProviders.Find(x=>x.Name == network.NetworkProvider)
                    .ToEither(Error.New($"network provider {network.NetworkProvider} not found"))
                    .ToAsync()

                let isFlatNetwork = networkProvider.Type == NetworkProviderType.Flat

                let c1 = new CancellationTokenSource(5000)

                from networkPort in GetOrAddAdapterPort(
                    network, message.Command.CatletId, cfg.AdapterName, c1.Token)

                let c2 = new CancellationTokenSource()

                from floatingPort in isFlatNetwork 
                    ? Prelude.RightAsync<Error, Unit>(Prelude.unit)
                    : GetOrAddFloatingPort(
                    networkPort, Option<string>.None,
                    "default", "default",
                    "default", c2.Token).ToAsync().Map(_ => Unit.Default)

                let fixedMacAddress =
                    message.Command.Config.NetworkAdapters.Find(x => x.Name == cfg.AdapterName)
                        .Map(x => x.MacAddress)
                        .IfNone("")
                let _ = UpdatePort(networkPort, cfg.AdapterName, fixedMacAddress)

                let c3 = new CancellationTokenSource()
                from ips in isFlatNetwork
                    ? Prelude.RightAsync<Error, IPAddress[]>(Array.Empty<IPAddress>())
                    : _ipManager.ConfigurePortIps(
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
                    AddressesV4 = string.Join(',', ips.Where(x => x.AddressFamily == AddressFamily.InterNetwork)),
                    AddressesV6 = string.Join(',', ips.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6))

                }

            )
            .TraverseParallel(l => l)
            .Map(settings => new UpdateCatletNetworksCommandResponse { NetworkSettings = settings.ToArray() })
            .FailOrComplete(_messaging, message);

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
            MacAddress = MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString()))
        };

        adapterPort.FloatingPort = port;

        return await _stateStore.For<FloatingNetworkPort>().AddAsync(port, cancellationToken);

    }


    private static Unit UpdatePort(CatletNetworkPort networkPort, string adapterName, string fixedMacAddress)
    {
        if (!string.IsNullOrEmpty(fixedMacAddress))
            networkPort.MacAddress = MacAddresses.FormatMacAddress(fixedMacAddress);
        else
        {
            networkPort.MacAddress ??= MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(
                $"{networkPort.CatletId.GetValueOrDefault()}_{adapterName}"));
        }

        return Unit.Default;
    }
}