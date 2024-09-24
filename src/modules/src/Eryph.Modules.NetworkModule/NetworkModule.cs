using System;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network;

[UsedImplicitly]
public class NetworkModule
{
    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.RegisterSingleton(serviceProvider.GetRequiredService<IAgentControlService>);
        container.RegisterSingleton<SyncedOVNDatabaseNode>();
        container.RegisterSingleton<NetworkControllerNode>();
        container.RegisterSingleton<IOVSService<SyncedOVNDatabaseNode>, OVSNodeService<SyncedOVNDatabaseNode>>();
        container.RegisterSingleton<IOVSService<NetworkControllerNode>, OVSNodeService<NetworkControllerNode>>();
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<OwnThreadOVSNodeHostedService<SyncedOVNDatabaseNode>>();
        options.AddHostedService<OwnThreadOVSNodeHostedService<NetworkControllerNode>>();

        options.AddLogging();
    }
}
