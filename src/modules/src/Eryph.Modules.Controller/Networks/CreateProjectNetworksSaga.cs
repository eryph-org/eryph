using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Networks
{
    [UsedImplicitly]
    internal class CreateProjectNetworksSaga : OperationTaskWorkflowSaga<CreateNetworksCommand, CreateProjectNetworksSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>

    {
        private readonly IStateStore _stateStore;

        public CreateProjectNetworksSaga(IBus bus, IOperationTaskDispatcher taskDispatcher,
            IStateStore stateStore) : base(bus, taskDispatcher)
        {
            _stateStore = stateStore;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CreateProjectNetworksSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override async Task Initiated(CreateNetworksCommand message)
        {
            Data.Config = message.Config;

            var project = await _stateStore.Read<Project>().GetBySpecAsync(
                new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, Data.Config.Project ?? "default"));

            if (project == null)
            {
                await Fail(new ErrorData { ErrorMessage = $"Project {Data.Config.Project} not found" });
                return;
            }

            var savedNetworks = await _stateStore
                .For<VirtualNetwork>()
                .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id));

            var foundNames = new List<string>();
            foreach (var networkConfig in Data.Config.Networks)
            {
                var savedNetwork = savedNetworks.Find(x => x.Name == networkConfig.Name);
                if (savedNetwork == null)
                {
                    var newNetwork = new VirtualNetwork
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = project.Id,
                        Name = networkConfig.Name,
                        Subnets = new List<VirtualNetworkSubnet>(),
                        NetworkPorts = new List<VirtualNetworkPort>(),
                    };

                    savedNetworks.Add(newNetwork);
                    await _stateStore.For<VirtualNetwork>().AddAsync(newNetwork);
                }

                foundNames.Add(networkConfig.Name);

            }

            var removeNetworks = savedNetworks.Where(x => !foundNames.Contains(x.Name));
            await _stateStore.For<VirtualNetwork>().DeleteRangeAsync(removeNetworks);

            // second pass - update of new or existing records
            foreach (var networkConfig in Data.Config.Networks.DistinctBy(x => x.Name))
            {
                var savedNetwork = savedNetworks.First(x => x.Name == networkConfig.Name);

                var providerName = networkConfig.Provider?.Name ?? "default";
                var providerSubnet = networkConfig.Provider?.Subnet ?? "default";
                var providerIpPool = networkConfig.Provider?.IpPool ?? "default";

                savedNetwork.NetworkProvider = providerName;
                savedNetwork.IpNetwork = networkConfig.Address;

                var providerPorts = savedNetwork.NetworkPorts
                    .Where(x => x is ProviderRouterPort).Cast<ProviderRouterPort>().ToArray();

                //remove all ports if more then one
                if (providerPorts.Length > 1)
                {
                    await _stateStore.For<ProviderRouterPort>().DeleteRangeAsync(providerPorts);
                    providerPorts = Array.Empty<ProviderRouterPort>();
                }

                var providerPort = providerPorts.FirstOrDefault();
                if (providerPort != null)
                {
                    if (providerPort.ProviderName != providerName ||
                        providerPort.SubnetName != providerSubnet ||
                        providerPort.PoolName != providerIpPool)
                    {
                        savedNetwork.NetworkPorts.Remove(providerPort);
                        providerPort = null;
                    }
                }

                if (providerPort == null)
                {
                    savedNetwork.NetworkPorts.Add(new ProviderRouterPort()
                    {
                        Name = "provider",
                        SubnetName = providerSubnet,
                        PoolName = providerIpPool,
                        MacAddress = MacAddresses.FormatMacAddress(
                            MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                        ProviderName = providerName,
                    });
                }

                var routerPorts = savedNetwork.NetworkPorts
                    .Where(x => x is NetworkRouterPort).Cast<NetworkRouterPort>().ToArray();

                //remove all ports if more then one
                if (routerPorts.Length > 1)
                {
                    await _stateStore.For<NetworkRouterPort>().DeleteRangeAsync(routerPorts);
                    routerPorts = Array.Empty<NetworkRouterPort>();
                }

                var routerPort = routerPorts.FirstOrDefault();
                if (routerPort != null && routerPort.Id != savedNetwork.RouterPort.Id)
                {
                    await _stateStore.For<NetworkRouterPort>().DeleteAsync(routerPort);
                    routerPort = null;
                }

                if (routerPort == null)
                {
                    var ipNetwork = IPNetwork.Parse(savedNetwork.IpNetwork);
                    routerPort = new NetworkRouterPort
                    {
                        MacAddress = MacAddresses.FormatMacAddress(
                            MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                        IpAssignments = new List<IpAssignment>(new[]
                        {
                            new IpAssignment
                            {
                                Id = Guid.NewGuid(),
                                IpAddress = ipNetwork.FirstUsable.ToString(),
                            }
                        }),
                        Name = "default",
                        RoutedNetworkId = savedNetwork.Id,
                        NetworkId = savedNetwork.Id,
                    };

                    savedNetwork.RouterPort = routerPort;
                    savedNetwork.NetworkPorts.Add(routerPort);
                }
                
                
                foundNames.Clear();

                foreach (var subnetConfig in networkConfig.Subnets)
                {
                    foundNames.Add(subnetConfig.Name);

                    var savedSubnet = savedNetwork.Subnets.FirstOrDefault(x => x.Name == 
                                                                               subnetConfig.Name);
                    if (savedSubnet == null)
                    {
                        savedNetwork.Subnets.Add(new VirtualNetworkSubnet
                        {
                            Name = subnetConfig.Name,
                            IpPools = new List<IpPool>(),
                            NetworkId = savedNetwork.Id
                        });
                    }
                }

                var removeSubnets = savedNetwork.Subnets.Where(x => !foundNames.Contains(x.Name));
                await _stateStore.For<VirtualNetworkSubnet>().DeleteRangeAsync(removeSubnets);

                foreach (var subnetConfig in networkConfig.Subnets.DistinctBy(x=>x.Name))
                {
                    var savedSubnet = savedNetwork.Subnets.First(x => x.Name == subnetConfig.Name);

                    savedSubnet.DhcpLeaseTime = 3600;
                    savedSubnet.MTU = subnetConfig.Mtu;
                    savedSubnet.DnsServersV4 = subnetConfig.DnsServers != null ? string.Join(',', subnetConfig.DnsServers) : null;
                    savedSubnet.IpNetwork = subnetConfig.Address ?? networkConfig.Address;

                    foundNames.Clear();

                    foreach (var ipPoolConfig in subnetConfig.IpPools.DistinctBy(x => x.Name))
                    {
                        foundNames.Add(ipPoolConfig.Name);

                        var savedIpPool = savedSubnet.IpPools.FirstOrDefault(x => x.Name == ipPoolConfig.Name);
                        if (savedIpPool == null)
                        {
                            savedSubnet.IpPools.Add(new IpPool
                            {
                                Name = ipPoolConfig.Name,
                                FirstIp = ipPoolConfig.FirstIp,
                                LastIp = ipPoolConfig.LastIp,
                                IpNetwork = subnetConfig.Address
                            });
                        }
                    }
                    var removeIpPools = savedSubnet.IpPools.Where(x => !foundNames.Contains(x.Name));
                    await _stateStore.For<IpPool>().DeleteRangeAsync(removeIpPools);

                }
            }

            await StartNewTask(new UpdateNetworksCommand
            {
                Projects = new[] { project.Id }
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }
    }
}