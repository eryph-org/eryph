﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
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
        from network in _stateStore.Read<VirtualNetwork>().IO.GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(command.ProjectId, networkConfig.Name, command.Config.Environment))
            .Bind(r =>
                // it is optional to have a environment specific network
                // therefore fallback to network in default environment if not found
                r.IsNone && command.Config.Environment != "default"
                    ? _stateStore.Read<VirtualNetwork>()
                        .IO.GetBySpecAsync(new VirtualNetworkSpecs.GetByName(command.ProjectId, networkConfig.Name,
                            "default"))
                        .Bind(fr => fr.ToEitherAsync(Error.New(
                            $"Network {networkConfig.Name} not found in environment {command.Config.Environment} and default environment.")))
                    : r.ToEitherAsync(Error.New(
                        $"Environments {command.Config.Environment}: Network {networkConfig.Name} not found.")))

        from networkProviders in _providerManager.GetCurrentConfiguration()
        from networkProvider in networkProviders.NetworkProviders.Find(x => x.Name == network.NetworkProvider)
            .ToEither(Error.New($"network provider {network.NetworkProvider} not found"))
            .ToAsync()

        let isFlatNetwork = networkProvider.Type == NetworkProviderType.Flat

        let c1 = new CancellationTokenSource(5000)

        from networkPort in GetOrAddAdapterPort(
            network, command.CatletId, catletMetadataId, networkConfig.AdapterName,
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
            ? Prelude.RightAsync<Error, IPAddress[]>(Array.Empty<IPAddress>())
            : _ipManager.ConfigurePortIps(
                command.ProjectId,
                command.Config.Environment ?? "default", networkPort,
                command.Config.Networks,
                c3.Token)

        from floatingIps in isFlatNetwork
            ? Prelude.RightAsync<Error, IPAddress[]>(Array.Empty<IPAddress>())
            : floatingPort.ToEither(Error.New("floating port is missing"))
                .ToAsync()
                .Bind(p => _providerIpManager.ConfigureFloatingPortIps(networkProvider, p, c3.Token))

        select new MachineNetworkSettings
        {
            NetworkProviderName = network.NetworkProvider,
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
                        CatletMetadataId = catletMetadataId,
                        Name = portName,
                        NetworkId = network.Id,
                        IpAssignments = new List<IpAssignment>()

                    };
                    return _stateStore.For<VirtualNetworkPort>().AddAsync(port, cancellationToken);
                }
            ).ConfigureAwait(false))
            .Map(p =>
            {
                p.AddressName = addressName;
                return (CatletNetworkPort)p;
            });

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
            networkPort.MacAddress ??= MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress(
                $"{catletId}_{adapterName}"));
        }

        return Unit.Default;
    }
}