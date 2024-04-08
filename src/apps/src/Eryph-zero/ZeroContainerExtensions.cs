using System;
using Dbosoft.OVN;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.VmHostAgent;
using Eryph.Rebus;
using Eryph.Runtime.Zero.Configuration.AgentSettings;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.Security.Cryptography;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting.WindowsServices;
using Rebus.Sagas;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using SimpleInjector;

namespace Eryph.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container, string ovsRunDir)
        {
            container
                .UseInMemoryBus()
                .UseSqlLite();

            container.Register<IRSAProvider, RSAProvider>();
            container.Register<ICryptoIOServices, WindowsCryptoIOServices>();
            container.Register<ICertificateGenerator, CertificateGenerator>();
            container.Register<ICertificateStoreService, WindowsCertificateStoreService>();
            container.RegisterInstance(new EryphOvsPathProvider(ovsRunDir));
            container.Register<IOVNSettings, LocalOVSWithOVNSettings>();
            container.Register<ISysEnvironment, EryphOVSEnvironment>();
            container.Register<INetworkProviderManager, NetworkProviderManager>();
            container.RegisterSingleton<INetworkSyncService, NetworkSyncServiceBridgeService>();
            container.RegisterSingleton<IAgentControlService, AgentControlService>();

            container.Register<IVmHostAgentConfigurationManager, VmHostAgentConfigurationManager>();
            container.Register<IHostSettingsProvider, HostSettingsProvider>();

            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish, 
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1)
            });
        }

        public static Container UseInMemoryBus(this Container container)
        {
            container.RegisterInstance(Info.Network);
            container.RegisterInstance(Info.SubscriberStore);
            container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
            container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
            container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();
            container.Register<IRebusConfigurer<ISubscriptionStorage>, DefaultSubscriptionStoreSelector>();

            return container;
        }

        public static Container UseSqlLite(this Container container)
        {
            container.RegisterInstance(new InMemoryDatabaseRoot());
            container.Register<IDbContextConfigurer<StateStoreContext>, SqlLiteStateStoreContextConfigurer>();
            return container;
        }

    }
}