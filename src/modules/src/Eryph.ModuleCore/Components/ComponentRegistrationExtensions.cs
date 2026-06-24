using System;
using System.Collections.Generic;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Startup;
using Rebus.Handlers;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.ModuleCore.Components;

public static class ComponentRegistrationExtensions
{
    /// <summary>
    /// Opts a module into controller-driven configuration distribution. The module
    /// registers as a component on startup, sends heartbeats, and applies
    /// configuration snapshots/pushes through its registered
    /// <see cref="IConfigRealizer"/>s. Each module calls this from its own DI setup.
    /// </summary>
    /// <remarks>
    /// The module must run a bus endpoint with the given <paramref name="inboundQueue"/>
    /// (transport configured with that queue name, not as a one-way client) so the
    /// controller can route configuration to it.
    /// </remarks>
    public static void AddComponentRegistration(
        this SimpleInjectorAddOptions options,
        ComponentType componentType,
        string inboundQueue,
        IReadOnlyDictionary<string, string>? advertisedEndpoints,
        params Type[] configRealizers)
    {
        var container = options.Container;

        container.RegisterSingleton(() => new ComponentIdentity(componentType, inboundQueue, advertisedEndpoints));
        container.RegisterSingleton<IComponentConfigState, ComponentConfigState>();
        container.Register<ConfigApplier>(Lifestyle.Scoped);

        // Host-supplied endpoint providers, resolved at registration time and merged with the static
        // endpoints above. Empty by default; a host appends one (Collection.Append) when the deployment
        // exposes endpoints — so the module never reads a flag to decide what it advertises.
        container.Collection.Register<IComponentEndpointProvider>(Array.Empty<Type>());

        // One realizer per configuration domain the module consumes (may be empty).
        container.Collection.Register<IConfigRealizer>(configRealizers);

        container.Collection.Append(
            typeof(IHandleMessages<ConfigSnapshotCommand>),
            typeof(ConfigSnapshotCommandHandler),
            Lifestyle.Scoped);
        container.Collection.Append(
            typeof(IHandleMessages<PushConfigCommand>),
            typeof(PushConfigCommandHandler),
            Lifestyle.Scoped);

        options.AddStartupHandler<ComponentRegistrationStartupHandler>();
        options.AddHostedService<ComponentHeartbeatService>();
    }
}
