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
public class UpdateCatletNetworksCommandHandler(
    ITaskMessaging messaging,
    ICatletIpManager ipManager,
    IStateStore stateStore,
    INetworkProviderManager providerManager,
    IProviderIpManager providerIpManager)
    : IHandleMessages<OperationTask<UpdateCatletNetworksCommand>>
{
    public async Task Handle(OperationTask<UpdateCatletNetworksCommand> message)
    {
        await messaging.ProgressMessage(message, "Updating Catlet network settings");

        await UpdateNetworks(message.Command)
            .Map(settings => new UpdateCatletNetworksCommandResponse
            {
                NetworkSettings = settings.ToArray()
            })
            .FailOrComplete(messaging, message);
    }

    public EitherAsync<Error, Seq<MachineNetworkSettings>> UpdateNetworks(
        UpdateCatletNetworksCommand command) =>
        from catletResult in stateStore.For<Catlet>().IO.GetByIdAsync(command.CatletId)
        from catlet in catletResult.ToEitherAsync(Error.New($"Catlet {command.CatletId} not found."))
        // TODO delete ports which are no longer configured
        from settings in command.Config.Networks
            .ToSeq()
            .Map(cfg => UpdateNetwork(catlet.MetadataId, command, cfg))
            .SequenceSerial()
        select settings;


    private EitherAsync<Error, MachineNetworkSettings> UpdateNetwork(
        Guid catletMetadataId,
        UpdateCatletNetworksCommand command,
        CatletNetworkConfig networkConfig) =>
        from environmentName in EnvironmentName.NewEither(
                Optional(command.Config.Environment)
                    .Filter(notEmpty)
                    .IfNone(EryphConstants.DefaultEnvironmentName))
            .ToAsync()
        from networkName in EryphNetworkName.NewEither(
                Optional(networkConfig.Name)
                    .Filter(notEmpty)
                    .IfNone(EryphConstants.DefaultNetworkName))
            .ToAsync()
        from network in stateStore.For<VirtualNetwork>().IO.GetBySpecAsync(
            new VirtualNetworkSpecs.GetByName(command.ProjectId, networkName.Value, environmentName.Value))
        // It is optional to have an environment specific network. Therefore,
        // we fall back to the network in the default environment.
        from validNetwork in network.IsNone && environmentName != EnvironmentName.New(EryphConstants.DefaultEnvironmentName)
            ? stateStore.For<VirtualNetwork>().IO.GetBySpecAsync(
                    new VirtualNetworkSpecs.GetByName(command.ProjectId, networkName.Value, EryphConstants.DefaultEnvironmentName))
                .Bind(fr => fr.ToEitherAsync(Error.New($"Network '{networkName}' not found in environment '{environmentName}' and default environment.")))
            : network.ToEitherAsync(Error.New($"Network '{networkName}' not found in environment '{environmentName}'."))

        from networkProviders in providerManager.GetCurrentConfiguration()
        from networkProvider in networkProviders.NetworkProviders.Find(x => x.Name == validNetwork.NetworkProvider)
            .ToEither(Error.New($"network provider {validNetwork.NetworkProvider} not found."))
            .ToAsync()

        let isFlatNetwork = networkProvider.Type == NetworkProviderType.Flat

        let fixedMacAddress = command.Config.NetworkAdapters
            .ToSeq()
            .Find(x => x.Name == networkConfig.AdapterName)
            .Bind(x => Optional(x.MacAddress))
        let c1 = new CancellationTokenSource(500000)

        

        from networkPort in AddOrUpdateAdapterPort(
            validNetwork, command.CatletId, catletMetadataId, networkConfig.AdapterName,
            command.Config.Hostname ?? command.Config.Name, fixedMacAddress, c1.Token)

        let c2 = new CancellationTokenSource()

        let providerSubnetName = validNetwork.Subnets

        from floatingPort in isFlatNetwork
            ? from _ in Optional(networkPort.FloatingPort)
                .Map(fp => stateStore.For<FloatingNetworkPort>().IO.DeleteAsync(fp))
                .Sequence()
              select Option<FloatingNetworkPort>.None
            : from providerPort in stateStore.For<ProviderRouterPort>().IO.GetBySpecAsync(
                new ProviderRouterPortSpecs.GetByNetworkId(validNetwork.Id))
              from validProviderPort in providerPort.ToEitherAsync(
                    Error.New($"The overlay network '{validNetwork.Name}' has no provider port."))
              from fp in UpdateFloatingPort(
                    networkPort, networkProvider.Name, validProviderPort.SubnetName, validProviderPort.PoolName, c2.Token)
                .ToAsync()
              select Some(fp)

        let c3 = new CancellationTokenSource()
        from ips in isFlatNetwork
            ? RightAsync<Error, IPAddress[]>([])
            : ipManager.ConfigurePortIps(
                validNetwork, networkPort,
                networkConfig,
                c3.Token)

        from floatingIps in isFlatNetwork
            ? RightAsync<Error, IPAddress[]>([])
            : floatingPort.ToEither(Error.New("floating port is missing"))
                .ToAsync()
                .Bind(p => providerIpManager.ConfigureFloatingPortIps(networkProvider.Name, p, c3.Token))

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

    private EitherAsync<Error, CatletNetworkPort> AddOrUpdateAdapterPort(
        VirtualNetwork network,
        Guid catletId,
        Guid catletMetadataId,
        string adapterName,
        string addressName,
        Option<string> fixedMacAddress,
        CancellationToken cancellationToken) =>
        from _ in RightAsync<Error, Unit>(unit)
        let portName = GetPortName(catletId, adapterName)
        let macAddress = fixedMacAddress
            .Filter(notEmpty)
            .IfNone(() => MacAddresses.GenerateMacAddress(portName))
        from existingPort in stateStore.For<CatletNetworkPort>().IO.GetBySpecAsync(
                new NetworkPortSpecs.GetByNetworkAndNameForCatlet(network.Id, portName), cancellationToken)
        from updatedPort in existingPort.Match(
                Some: p => from _ in RightAsync<Error, Unit>(unit)
                            let __ =  fun(() =>
                            {
                                p.AddressName = addressName;
                                p.MacAddress = MacAddresses.FormatMacAddress(macAddress);
                                p.Network = network;
                            })
                           select p,
                None: () => from _ in RightAsync<Error, Unit>(unit)
                    let newPort = new CatletNetworkPort
                    {
                        Id = Guid.NewGuid(),
                        CatletMetadataId = catletMetadataId,
                        Name = portName,
                        MacAddress = MacAddresses.FormatMacAddress(macAddress),
                        Network = network,
                        AddressName = addressName,
                        IpAssignments = [],
                    }
                    from addedPort in stateStore.For<CatletNetworkPort>().IO.AddAsync(newPort, cancellationToken)
                            select addedPort)
        select updatedPort;

    

    private async Task<Either<Error, FloatingNetworkPort>> UpdateFloatingPort(
        CatletNetworkPort adapterPort,
        string providerName,
        string providerSubnetName,
        string providerPoolName,
        CancellationToken cancellationToken)
    {
        await stateStore.LoadPropertyAsync(adapterPort, x => x.FloatingPort, cancellationToken);

        if (adapterPort.FloatingPort != null)
        {
            var floatingPort = adapterPort.FloatingPort;
            if (floatingPort.ProviderName != providerName || floatingPort.SubnetName !=
                providerSubnetName || floatingPort.PoolName != providerPoolName)
            {
                adapterPort.FloatingPort = null;
                await stateStore.For<FloatingNetworkPort>().DeleteAsync(floatingPort, cancellationToken);
            }

        }

        if (adapterPort.FloatingPort != null)
            return adapterPort.FloatingPort;

        var port = new FloatingNetworkPort
        {
            Id = Guid.NewGuid(),
            Name = adapterPort.Name,
            ProviderName = providerName,
            SubnetName = providerSubnetName,
            PoolName = providerPoolName,
            MacAddress = MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString()))
        };

        adapterPort.FloatingPort = port;

        return await stateStore.For<FloatingNetworkPort>().AddAsync(port, cancellationToken);
    }

    // TODO Remove port and attached floating port if necessary
    private EitherAsync<Error, Unit> RemovePort() => Error.New("not implemented");

    private EitherAsync<Error, Unit> RemoveFloatingPort() => Error.New("not implemented");

    private static string GetPortName(Guid catletId, string adapterName) =>
        $"{catletId}_{adapterName}";
}
