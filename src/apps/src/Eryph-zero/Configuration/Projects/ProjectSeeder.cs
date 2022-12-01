using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;

namespace Eryph.Runtime.Zero.Configuration.Project
{
    [UsedImplicitly]
    internal class ProjectSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly IStateStoreRepository<Tenant> _tenantRepository;

        private readonly IStateStoreRepository<StateDb.Model.Project> _projectRepository;
        private readonly IStateStoreRepository<VirtualNetwork> _networkRepository;

        public ProjectSeeder(IStateStoreRepository<StateDb.Model.Project> projectRepository,
            IStateStoreRepository<VirtualNetwork> networkRepository, IStateStoreRepository<Tenant> tenantRepository)
        {
            _projectRepository = projectRepository;
            _networkRepository = networkRepository;
            _tenantRepository = tenantRepository;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            var tenantId = Guid.Parse("{C1813384-8ECB-4F17-B846-821EE515D19B}");

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, stoppingToken);

            if (tenant == null)
            {
                tenant = new Tenant { Id = tenantId };
                await _tenantRepository.AddAsync(tenant, stoppingToken);
                await _tenantRepository.SaveChangesAsync(stoppingToken);
            }

            var project = await _projectRepository.GetBySpecAsync(
                new ProjectSpecs.GetByName(tenantId, "default")
                , stoppingToken);

            if (project == null)
            {
                project = new StateDb.Model.Project
                {
                    Id = Guid.NewGuid(),
                    Name = "default",
                    TenantId = tenantId
                };
                await _projectRepository.AddAsync(project, stoppingToken);
            }

            await _projectRepository.SaveChangesAsync(stoppingToken);
            var projectId = project.Id;

            var network = await _networkRepository.GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(projectId, "default")
                , stoppingToken);

            if (network == null)
            {
                network = new VirtualNetwork
                {
                    Id = Guid.NewGuid(),
                    Name = "default",
                    ProjectId = projectId,
                    NetworkProvider = "default",
                    NetworkPorts = new List<VirtualNetworkPort>
                    {
                        new ProviderNetworkPort() { Name = "provider", Id = Guid.NewGuid()}

                    },
                    Subnets = new List<VirtualNetworkSubnet>(new[]{new VirtualNetworkSubnet
                    {
                        Id = Guid.NewGuid(),
                        IpNetwork = "10.0.0.0/20",
                        Name = "default",
                        IpPools = new List<IpPool>(new []
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
                    }})
                };
                await _networkRepository.AddAsync(network, stoppingToken);
            }

            try
            {
                await _networkRepository.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {

            }
        }
    }
}