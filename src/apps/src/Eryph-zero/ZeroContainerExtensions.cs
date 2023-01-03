using System.Reflection;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.VmHostAgent;
using Eryph.Rebus;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.Security.Cryptography;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore.Storage;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;

namespace Eryph.Runtime.Zero
{
    public static class Info
    {
        public static InMemNetwork Network = new InMemNetwork();
    }

    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container
                .UseInMemoryBus()
                .UseSqlLite();

            container.Register<IRSAProvider, RSAProvider>();
            container.Register<ICryptoIOServices, WindowsCryptoIOServices>();
            container.Register<ICertificateGenerator, CertificateGenerator>();
            container.Register<ICertificateStoreService, WindowsCertificateStoreService>();

            container.Register<IOVNSettings, LocalOVSWithOVNSettings>();
            container.Register<ISysEnvironment, SystemEnvironment>();
            container.Register<INetworkProviderManager, NetworkProviderManager>();
            container.RegisterSingleton<INetworkSyncService, NetworkSyncServiceBridgeService>();
            container.RegisterSingleton<IAgentControlService, AgentControlService>();
        }

        public static Container UseInMemoryBus(this Container container)
        {
            container.RegisterInstance(Info.Network);
            container.RegisterInstance(new InMemorySubscriberStore());
            container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            container.Register<IRebusSagasConfigurer, FileSystemSagasConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>();
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