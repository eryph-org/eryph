using System;
using System.IO;
using Dbosoft.OVN;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.Rebus;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.AgentSettings;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.Security.Cryptography;
using Eryph.StateDb;
using Eryph.StateDb.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Sagas;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;

namespace Eryph.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container.RegisterInstance(Info.Network);
            
            container.UseSqlLite();

            container.Register<ICertificateKeyPairGenerator, WindowsCertificateKeyPairPairGenerator>();
            container.Register<IRSAProvider, RSAProvider>();
            container.Register<ICryptoIOServices, WindowsCryptoIOServices>();
            container.Register<ICertificateGenerator, CertificateGenerator>();
            container.Register<ICertificateStoreService, WindowsCertificateStoreService>();
            container.RegisterInstance<IEryphOvsPathProvider>(new EryphOvsPathProvider());
            container.Register<IOVNSettings, LocalOVSWithOVNSettings>();
            container.Register<ISysEnvironment, EryphOVSEnvironment>();
            container.Register<INetworkProviderManager, NetworkProviderManager>();
            container.RegisterSingleton<INetworkSyncService, NetworkSyncServiceBridgeService>();
            container.RegisterSingleton<IAgentControlService, AgentControlService>();

            container.Register<IVmHostAgentConfigurationManager, VmHostAgentConfigurationManager>();
            container.RegisterSingleton<IGenePoolApiKeyStore, ZeroGenePoolApiKeyStore>();
            container.Register<IHostSettingsProvider, HostSettingsProvider>();
            container.RegisterSingleton<IApplicationInfoProvider, ZeroApplicationInfoProvider>();

            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish, 
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1),
                JsonSerializerOptions = EryphJsonSerializerOptions.Options,
            });
        }

        public static Container UseInMemoryBus(this Container container, IServiceProvider serviceProvider)
        {
            container.RegisterInstance(serviceProvider.GetRequiredService<InMemNetwork>());
            container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
            container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
            container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();

            return container;
        }

        public static Container UseSqlLite(this Container container)
        {
            container.RegisterInstance(new InMemoryDatabaseRoot());
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(ZeroConfig.GetPrivateConfigPath(), "state.db"),
            };
            container.RegisterInstance<IStateStoreContextConfigurer>(
                new SqliteStateStoreContextConfigurer(builder.ToString()));
            return container;
        }
    }
}