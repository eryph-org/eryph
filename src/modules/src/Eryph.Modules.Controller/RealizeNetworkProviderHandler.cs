using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.OVN;
using Dbosoft.OVN.OSCommands.OVN;
using Eryph.Core;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.VmManagement.Networking.Settings;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller;

public class RealizeNetworkProviderHandler : IHostedServiceHandler
{
    private readonly Container _container;

    public RealizeNetworkProviderHandler(Container container)
    {
        _container = container;
    }
    public async Task Execute(CancellationToken stoppingToken)
    {
        await using var scope = AsyncScopedLifestyle.BeginScope(_container);

        var stateStore = scope.GetInstance<IStateStore>();
        var providerManager = scope.GetInstance<INetworkProviderManager>();

        var config = (await providerManager.GetCurrentConfiguration().ToEither()).IfLeft(new NetworkProvidersConfiguration());

        var existingSubnets = await stateStore.For<ProviderSubnet>()
            .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets(), stoppingToken);

        var existingIpPools = existingSubnets.SelectMany(x => x.IpPools).ToArray();
        var foundSubnets = new List<ProviderSubnet>();
        var foundIpPools = new List<IpPool>();

        foreach (var networkProvider in config.NetworkProviders.Where(x=>x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay))
        {
            foreach (var subnet in networkProvider.Subnets)
            {
                var subnetEntity = existingSubnets.FirstOrDefault(e =>
                    e.ProviderName == networkProvider.Name && e.Name == subnet.Name);

                if (subnetEntity != null)
                {
                    foundSubnets.Add(subnetEntity);
                    subnetEntity.IpNetwork = subnet.Network;
                }
                else
                {
                    subnetEntity = new ProviderSubnet
                    {
                        Id = Guid.NewGuid(),
                        IpNetwork = subnet.Network,
                        Name = subnet.Name,
                        ProviderName = networkProvider.Name, 
                        IpPools = new List<IpPool>()
                    };

                    await stateStore.For<ProviderSubnet>().AddAsync(subnetEntity, stoppingToken);
                }
                    

                foreach (var ipPool in subnet.IpPools)
                {
                    var ipPoolEntity = subnetEntity.IpPools.FirstOrDefault(x =>
                        x.Name == ipPool.Name && x.FirstIp == ipPool.FirstIp && x.LastIp == ipPool.LastIp && x.IpNetwork == subnetEntity.IpNetwork);

                    if (ipPoolEntity != null)
                        foundIpPools.Add(ipPoolEntity);
                    else
                    {
                        await stateStore.For<IpPool>().AddAsync(new IpPool
                        {
                            Id = Guid.NewGuid(),
                            FirstIp = ipPool.FirstIp,
                            LastIp = ipPool.LastIp,
                            Counter = 0,
                            IpNetwork = subnet.Network,
                            Name = ipPool.Name,
                            SubnetId = subnetEntity.Id
                        }, stoppingToken);
                    }

                }

            }                                    
        }

        var removePools = existingIpPools.Where(e => foundIpPools.All(x => x.Id != e.Id)).ToArray();
        var removeSubnets = existingSubnets.Where(e => foundSubnets.All(x => x.Id != e.Id)).ToArray();

        await stateStore.For<IpPool>().DeleteRangeAsync(removePools, stoppingToken);
        await stateStore.For<ProviderSubnet>().DeleteRangeAsync(removeSubnets, stoppingToken);
        await stateStore.For<ProviderSubnet>().SaveChangesAsync(stoppingToken);


        var projects = (await stateStore.For<Project>().ListAsync(stoppingToken)).First();
        var res = await UpdateProjectNetworkPlan(projects.Id);
        await stateStore.For<ProviderSubnet>().SaveChangesAsync(stoppingToken);


    }


    private EitherAsync<Error, Unit> UpdateProjectNetworkPlan(Guid projectId)
    {
        var sysEnv = _container.GetInstance<ISysEnvironment>();
        var ovnSettings = _container.GetInstance<IOVNSettings>();
        var planBuilder = _container.GetInstance<IProjectNetworkPlanBuilder>();
        var logger = _container.GetInstance<ILogger>();

        var networkplanRealizer =
            new NetworkPlanRealizer(new OVNControlTool(sysEnv, ovnSettings.NorthDBConnection), logger);

        
        return from networkPlan in planBuilder.GenerateNetworkPlan(projectId, CancellationToken.None)
            from _ in networkplanRealizer.ApplyNetworkPlan(networkPlan)
            select Unit.Default;
    }
}