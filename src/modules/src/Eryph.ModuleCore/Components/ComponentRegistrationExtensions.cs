using System;
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
        string inboundQueue)
    {
        Register(options, componentType, inboundQueue, Array.Empty<Type>());
    }

    /// <summary>
    /// As <see cref="AddComponentRegistration(SimpleInjectorAddOptions,ComponentType,string)"/>,
    /// additionally registering a capabilities provider the module uses to advertise
    /// its settings (e.g. datastores/environments) to the controller at registration.
    /// </summary>
    public static void AddComponentRegistration<TCapabilitiesProvider>(
        this SimpleInjectorAddOptions options,
        ComponentType componentType,
        string inboundQueue)
        where TCapabilitiesProvider : class, IComponentCapabilitiesProvider
    {
        options.Container.Register<TCapabilitiesProvider>(Lifestyle.Scoped);
        Register(options, componentType, inboundQueue, [typeof(TCapabilitiesProvider)]);
    }

    private static void Register(
        SimpleInjectorAddOptions options,
        ComponentType componentType,
        string inboundQueue,
        Type[] capabilitiesProviders)
    {
        var container = options.Container;

        container.RegisterSingleton(() => new ComponentIdentity(componentType, inboundQueue));
        container.RegisterSingleton<IComponentConfigState, ComponentConfigState>();
        container.Register<ConfigApplier>(Lifestyle.Scoped);

        container.Collection.Register<IComponentCapabilitiesProvider>(capabilitiesProviders);
        // Realizers are appended by the module per domain it consumes; none yet.
        container.Collection.Register<IConfigRealizer>(Array.Empty<Type>());

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
