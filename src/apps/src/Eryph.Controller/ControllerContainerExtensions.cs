using System;
using Dbosoft.Rebus.Operations;
using Eryph.AppCore;
using Eryph.Core;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using SimpleInjector;

namespace Eryph.Controller
{
    internal static class ControllerContainerExtensions
    {
        /// <summary>
        /// Root-container registrations that the controller module resolves through the
        /// cross-wired service provider. Module-container-resolved dependencies (bus
        /// transport, Rebus stores, the state-store context + migration) are registered
        /// via the host filters in <see cref="HostControllerModuleExtensions"/>.
        /// </summary>
        public static void Bootstrap(this Container container)
        {
            container.Register<IControllerSettingsManager, ControllerSettingsManager>();
            container.Register<INetworkProviderManager, NetworkProviderManager>();

            container.RegisterInstance<IStateStoreContextConfigurer>(
                new MySqlStateStoreContextConfigurer(GetStateDbConnectionString()));

            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish,
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1),
                JsonSerializerOptions = EryphJsonSerializerOptions.Options,
            });
        }

        /// <summary>The MariaDB connection string, from env or a localhost dev default.</summary>
        public static string GetStateDbConnectionString() =>
            Environment.GetEnvironmentVariable("ERYPH_STATEDB_CONNECTIONSTRING")
            ?? "Server=localhost;Port=3306;Database=eryph;Uid=root;Pwd=eryph;";
    }
}
