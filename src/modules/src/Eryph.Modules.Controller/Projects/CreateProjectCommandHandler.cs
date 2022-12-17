using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Projects;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Projects
{
    internal class CreateProjectCommandHandler : IHandleMessages<OperationTask<CreateProjectCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly IBus _bus;

        public CreateProjectCommandHandler(IStateStore stateStore, IBus bus)
        {
            _stateStore = stateStore;
            _bus = bus;
        }

        public async Task Handle(OperationTask<CreateProjectCommand> message)
        {
            var stoppingToken = new CancellationTokenSource(10000);

            var project = await _stateStore.For<Project>().AddAsync(
                new Project
                {
                    Id = message.Command.CorrelationId, Name = message.Command.Name,
                    TenantId = EryphConstants.DefaultTenantId
                }, stoppingToken.Token);

            if (!message.Command.NoDefaultNetwork)
            {
                var networkId = Guid.NewGuid();
                var routerPort = new NetworkRouterPort
                {
                    Id = Guid.NewGuid(),
                    MacAddress = MacAddresses.FormatMacAddress(
                        MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                    IpAssignments = new List<IpAssignment>(new[]
                    {
                        new IpAssignment
                        {
                            Id = Guid.NewGuid(),
                            IpAddress = "10.0.0.1",
                        }
                    }),
                    Name = "default",
                    RoutedNetworkId = networkId,
                    NetworkId = networkId,
                };

                var network = new VirtualNetwork
                {
                    Id = networkId,
                    Name = "default",
                    ProjectId = project.Id,
                    IpNetwork = "10.0.0.0/20",
                    NetworkProvider = "default",
                    RouterPort = routerPort,
                    NetworkPorts = new List<VirtualNetworkPort>
                    {
                        routerPort,
                        new ProviderRouterPort()
                        {
                            Name = "provider",
                            Id = Guid.NewGuid(),
                            SubnetName = "default",
                            PoolName = "default",
                            MacAddress = MacAddresses.FormatMacAddress(
                                MacAddresses.GenerateMacAddress(Guid.NewGuid().ToString())),
                        }

                    },
                    Subnets = new List<VirtualNetworkSubnet>(new[]
                    {
                        new VirtualNetworkSubnet
                        {
                            Id = Guid.NewGuid(),
                            IpNetwork = "10.0.0.0/20",
                            Name = "default",
                            DhcpLeaseTime = 3600,
                            MTU = 1400,
                            DnsServersV4 = "9.9.9.9 8.8.8.8",
                            IpPools = new List<IpPool>(new[]
                            {
                                new IpPool
                                {
                                    Id = Guid.NewGuid(),
                                    Name = "default",
                                    IpNetwork = "10.0.0.0/20",
                                    Counter = 0,
                                    FirstIp = "10.0.0.100",
                                    LastIp = "10.0.2.240"
                                }
                            })
                        }
                    })
                };

                await _stateStore.For<VirtualNetwork>().AddAsync(network, stoppingToken.Token);
            }

            await _bus.CompleteTask(message, new ProjectReference { ProjectId = message.Command.CorrelationId });
        }
    }
}
