using System;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.ModuleCore.Networks;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using SimpleInjector;

namespace Eryph.Controller;

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

        // Bridges the OVN control plane to the host-agent chassis. In the split runtime
        // the controller has no in-process chassis, so this stays recipient-less (the
        // network module resolves it from the cross-wired host service provider).
        container.RegisterSingleton<IAgentControlService, AgentControlService>();

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

    /// <summary>
    /// The MariaDB connection string, supplied via the <c>ERYPH_STATEDB_CONNECTIONSTRING</c>
    /// environment variable. No credentialed default is hardcoded.
    /// </summary>
    public static string GetStateDbConnectionString() =>
        Environment.GetEnvironmentVariable("ERYPH_STATEDB_CONNECTIONSTRING")
        ?? throw new InvalidOperationException(
            "The state database connection string must be provided via the "
            + "ERYPH_STATEDB_CONNECTIONSTRING environment variable.");
}
