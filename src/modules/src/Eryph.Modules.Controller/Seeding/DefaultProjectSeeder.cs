using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero.Configuration.Projects;

[UsedImplicitly]
internal class DefaultProjectSeeder : IConfigSeeder<ControllerModule>
{
    private readonly ILogger _logger;
    private readonly IStateStore _stateStore;
    private readonly IDefaultNetworkConfigRealizer _defaultNetworkConfigRealizer;

    public DefaultProjectSeeder(
        ILogger logger,
        INetworkProviderManager networkProviderManager,
        IStateStore stateStore, 
        IDefaultNetworkConfigRealizer defaultNetworkConfigRealizer)
    {
        _logger = logger;
        _stateStore = stateStore;
        _defaultNetworkConfigRealizer = defaultNetworkConfigRealizer;
    }

    public async Task Execute(CancellationToken stoppingToken)
    {
        var tenantId = EryphConstants.DefaultTenantId;

        var project = await _stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetByName(tenantId, "default")
            , stoppingToken);

        if (project == null)
        {
            _logger.LogInformation("Default project '{projectId}' not found in state db. Creating project record.", EryphConstants.DefaultProjectId);

            project = new Project
            {
                Id = EryphConstants.DefaultProjectId,
                Name = "default",
                TenantId = tenantId
            };
            await _stateStore.For<Project>().AddAsync(project, stoppingToken);
        }

        await _stateStore.SaveChangesAsync(stoppingToken);

        var network = await _stateStore.For<VirtualNetwork>().GetBySpecAsync(
            new VirtualNetworkSpecs.GetByName(project.Id, "default", "default"),
            stoppingToken);


        if (network == null)
        {
            _logger.LogInformation("Default network not found in state db. Creating network record.");

            await _defaultNetworkConfigRealizer.RealizeDefaultConfig(project.Id);
            await _stateStore.SaveChangesAsync(stoppingToken);
        }
    }
}
