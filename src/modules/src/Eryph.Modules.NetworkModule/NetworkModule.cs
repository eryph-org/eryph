using System;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network
{
    [UsedImplicitly]
    public class NetworkModule
    {
        public string Name => "Eryph.Network";


        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider sp, IServiceCollection services)
        {
            services.AddSingleton(sp.GetRequiredService<ISysEnvironment>());
            services.AddSingleton(sp.GetRequiredService<IOVNSettings>());

            services.AddOvsNode<OVNDatabaseNode>();
            services.AddOvsNode<NetworkControllerNode>();

        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {


            options.AddHostedService<OVSNodeHostedService<OVNDatabaseNode>>();
            options.AddHostedService<OVSNodeHostedService<NetworkControllerNode>>();

            options.AddLogging();
        }

    }
}