using System;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using Rebus.Sagas;
using Rebus.Timeouts;
using SimpleInjector;

namespace Eryph.Controller
{
    internal static class ControllerContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container
                .UseRabbitMq()
                .UseMySql();

            // Controller-owned config managers (shared standalone implementations).
            // Cross-wired so ControllerModule can resolve them from the service provider.
            container.Register<IControllerSettingsManager, AppCore.ControllerSettingsManager>();
            container.Register<INetworkProviderManager, AppCore.NetworkProviderManager>();

            // Rebus operations workflow config (cross-wired to the MS service provider,
            // which ControllerModule resolves it from). Mirrors eryph-zero.
            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish,
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1),
                JsonSerializerOptions = EryphJsonSerializerOptions.Options,
            });
        }

        public static Container UseRabbitMq(this Container container)
        {
            container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();

            return container;
        }

        public static Container UseMySql(this Container container)
        {
            // State database on MariaDB. Connection string via env var so the same
            // binary can target different brokers/databases per deployment.
            var connectionString = Environment.GetEnvironmentVariable("ERYPH_STATEDB_CONNECTIONSTRING")
                ?? "Server=localhost;Port=3306;Database=eryph;Uid=root;Pwd=eryph;";
            container.RegisterInstance<IStateStoreContextConfigurer>(
                new MySqlStateStoreContextConfigurer(connectionString));

            // Phase-1 milestone: in-memory Rebus stores (saga/timeout) selected via the
            // 'store:type=inmemory' configuration. Durable MariaDB-backed Rebus stores
            // are a later milestone.
            container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
            container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();

            return container;
        }
    }
}