using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Sagas.Exclusive;

using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network
{
    [UsedImplicitly]
    public class NetworkModule
    {
        public string Name => "Eryph.Network";


        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {

            options.Services.AddSingleton<ISysEnvironment, SystemEnvironment>();
            options.Services.AddSingleton<IOVNSettings, LocalOVSWithOVNSettings>();

            options.Services.AddOvsNode<OVNDatabaseNode>();
            options.Services.AddOvsNode<NetworkControllerNode>();

            options.AddHostedService<OVSNodeHostedService<OVNDatabaseNode>>();
            options.AddHostedService<OVSNodeHostedService<NetworkControllerNode>>();

            options.AddLogging();
        }

    }
}