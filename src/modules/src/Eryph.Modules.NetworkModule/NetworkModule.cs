using System;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network
{
    [UsedImplicitly]
    public class NetworkModule
    {

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider sp, IServiceCollection services)
        {
            services.AddSingleton(sp.GetRequiredService<ISysEnvironment>());
            services.AddSingleton(sp.GetRequiredService<IOVNSettings>());
            services.AddSingleton(sp.GetRequiredService<IAgentControlService>());

            services.AddOvsNode<SyncedOVNDatabaseNode>();
            services.AddOvsNode<NetworkControllerNode>();

        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {


            options.AddHostedService<OVSNodeHostedService<SyncedOVNDatabaseNode>>();
            options.AddHostedService<OVSNodeHostedService<NetworkControllerNode>>();

            options.AddLogging();
        }

    }
}