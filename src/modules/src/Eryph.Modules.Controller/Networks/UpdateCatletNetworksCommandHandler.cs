using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.ClassInstances;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

#pragma warning restore 1998
[UsedImplicitly]
public class UpdateCatletNetworksCommandHandler : IHandleMessages<OperationTask<UpdateCatletNetworksCommand>>
{
    private readonly ITaskMessaging _messaging;
    private readonly IStateStore _stateStore;
    private readonly ICatletIpManager _ipManager;
    private readonly IProviderIpManager _providerIpManager;
    private readonly INetworkProviderManager _providerManager;


    public UpdateCatletNetworksCommandHandler(ITaskMessaging messaging, ICatletIpManager ipManager, 
        IStateStore stateStore, INetworkProviderManager providerManager, IProviderIpManager providerIpManager)
    {
        _messaging = messaging;
        _ipManager = ipManager;
        _stateStore = stateStore;
        _providerManager = providerManager;
        _providerIpManager = providerIpManager;
    }

    public async Task Handle(OperationTask<UpdateCatletNetworksCommand> message)
    {
        await _messaging.ProgressMessage(message, "Updating Catlet network settings");

        await UpdateNetworks(message.Command)
            .Map(settings => new UpdateCatletNetworksCommandResponse
            {
                NetworkSettings = settings.ToArray()
            })
            .FailOrComplete(_messaging, message);
    }

    private EitherAsync<Error, Seq<MachineNetworkSettings>> UpdateNetworks(
        UpdateCatletNetworksCommand command) =>
        from catletResult in _stateStore.For<Catlet>().IO.GetByIdAsync(command.CatletId)
        from catlet in catletResult.ToEitherAsync(Error.New($"Catlet {command.CatletId} not found."))
        from settings in command.Config.Networks
            .ToSeq()
            .Map(cfg => UpdateNetwork(catlet.MetadataId, command, cfg))
            .SequenceSerial()
        select settings;


    private EitherAsync<Error, MachineNetworkSettings> UpdateNetwork(
        Guid catletMetadataId,
        UpdateCatletNetworksCommand command,
        CatletNetworkConfig networkConfig) =>
        from environmentName in EnvironmentName.NewEither(command.Config.Environment)
            .ToAsync()
        from networkName in EryphNetworkName.NewEither(networkConfig.Name)
            .ToAsync()
        from network in _stateStore.Read<VirtualNetwork>().IO.GetBySpecAsync(
            new VirtualNetworkSpecs.GetByName(command.ProjectId, networkName.Value, environmentName.Value))
        // It is optional to have an environment specific network. Therefore,
        // we fall back to the network in the default environment.
        from validNetwork in network.IsNone && environmentName != EnvironmentName.New(EryphConstants.DefaultEnvironmentName)
            ? _stateStore.Read<VirtualNetwork>().IO.GetBySpecAsync(
                    new VirtualNetworkSpecs.GetByName(command.ProjectId, networkName.Value, EryphConstants.DefaultEnvironmentName))
                .Bind(fr => fr.ToEitherAsync(Error.New($"Network {networkName} not found in environment {environmentName} and default environment.")))
            : network.ToEitherAsync(Error.New($"Environment {environmentName}: Network {networkName} not found."))

        from networkProviders in _providerManager.GetCurrentConfiguration()
        from networkProvider in networkProviders.NetworkProviders.Find(x => x.Name == validNetwork.NetworkProvider)
            .ToEither(Error.New($"network provider {validNetwork.NetworkProvider} not found."))
            .ToAsync()

        let isFlatNetwork = networkProvider.Type == NetworkProviderType.Flat

        let c1 = new CancellationTokenSource(500000)

        // TODO update assignment of port to network when the network changed in the config
        // TODO delete ports which are no longer configured

        from networkPort in GetOrAddAdapterPort(
            validNetwork, command.CatletId, catletMetadataId, networkConfig.AdapterName,
            command.Config.Hostname ?? command.Config.Name, c1.Token)

        let c2 = new CancellationTokenSource()

        from floatingPort in isFlatNetwork
            ? Prelude.RightAsync<Error, Option<FloatingNetworkPort>>(Option<FloatingNetworkPort>.None)
            : GetOrAddFloatingPort(
                    networkPort, Option<string>.None,
                    "default", "default",
                    "default", c2.Token)
                .ToAsync()
                .Map(Option<FloatingNetworkPort>.Some)

        let fixedMacAddress =
            command.Config.NetworkAdapters.Find(x => x.Name == networkConfig.AdapterName)
                .Bind(x => Prelude.Optional(x.MacAddress))
                .IfNone("")
        let _ = UpdatePort(networkPort, command.CatletId, networkConfig.AdapterName, fixedMacAddress)

        let c3 = new CancellationTokenSource()
        from ips in isFlatNetwork
            ? Prelude.RightAsync<Error, IPAddress[]>([])
            : _ipManager.ConfigurePortIps(
                command.ProjectId,
                command.Config.Environment ?? "default", networkPort,
                networkConfig,
                c3.Token)

        from floatingIps in isFlatNetwork
            ? Prelude.RightAsync<Error, IPAddress[]>([])
            : floatingPort.ToEither(Error.New("floating port is missing"))
                .ToAsync()
                .Bind(p => _providerIpManager.ConfigureFloatingPortIps(networkProvider, p, c3.Token))

        select new MachineNetworkSettings
        {
            NetworkProviderName = validNetwork.NetworkProvider,
            NetworkName = networkConfig.Name,
            AdapterName = networkConfig.AdapterName,
            PortName = networkPort.Name,
            MacAddress = networkPort.MacAddress,
            AddressesV4 = string.Join(',', ips.Where(x => x.AddressFamily == AddressFamily.InterNetwork)),
            FloatingAddressV4 = floatingIps.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                ?.ToString(),
            AddressesV6 = string.Join(',', ips.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6)),
            FloatingAddressV6 = floatingIps.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6)
                ?.ToString(),
        };

    private EitherAsync<Error, CatletNetworkPort> GetOrAddAdapterPort(
        VirtualNetwork network,
        Guid catletId,
        Guid catletMetadataId,
        string adapterName,
        string addressName,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let portName = GetPortName(catletId, adapterName)
        from existingPort in _stateStore.For<CatletNetworkPort>().IO.GetBySpecAsync(
                new NetworkPortSpecs.GetByNetworkAndNameForCatlet(network.Id, portName), cancellationToken)
        from updatedPort in existingPort.Match(
                Some: p => from _ in RightAsync<Error, Unit>(unit)
                            let __ =  fun(() =>
                            {
                                p.AddressName = addressName;
                                // TODO use proper fixed MAC address
                                p.MacAddress = string.IsNullOrEmpty(null)
                                    ? MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(portName))
                                    : MacAddresses.FormatMacAddress("a");
                            })
                           select p,
                None: () => from _ in RightAsync<Error, Unit>(unit)
                    let newPort = new CatletNetworkPort
                    {
                        Id = Guid.NewGuid(),
                        CatletMetadataId = catletMetadataId,
                        Name = portName,
                        // TODO use proper fixed MAC address
                        MacAddress = string.IsNullOrEmpty(null)
                            ? MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(portName))
                            : MacAddresses.FormatMacAddress("a"),
                        NetworkId = network.Id,
                        AddressName = addressName,
                        IpAssignments = new List<IpAssignment>()
                    }
                    from addedPort in _stateStore.For<CatletNetworkPort>().IO.AddAsync(newPort, cancellationToken)
                            select addedPort)
        select updatedPort;

    

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


    private static Unit UpdatePort(
        CatletNetworkPort networkPort,
        Guid catletId,
        string adapterName,
        string fixedMacAddress)
    {
        if (!string.IsNullOrEmpty(fixedMacAddress))
            networkPort.MacAddress = MacAddresses.FormatMacAddress(fixedMacAddress);
        else
        {
            networkPort.MacAddress ??= MacAddresses.FormatMacAddress(
                MacAddresses.GenerateMacAddress(GetPortName(catletId, adapterName)));
        }

        return Unit.Default;
    }


    private static string GetPortName(Guid catletId, string adapterName) =>
        $"{catletId}_{adapterName}";
}