using System;
using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Identity;
using Eryph.Rebus;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Subscriptions;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Identity
{
    /// <summary>
    /// Makes the standalone identity runtime a deployment component: it runs a RabbitMQ bus
    /// endpoint on its own queue, registers with the controller, and advertises its identity
    /// endpoint (the producer side of the Endpoints config domain). The identity module itself
    /// has no bus, so the transport and component registration are added here via host filters.
    /// </summary>
    internal static class HostIdentityModuleExtensions
    {
        private static string InboundQueue => $"{QueueNames.IdentityServices}.{Environment.MachineName}";

        public static IModulesHostBuilder ConfigureIdentityComponent(this IModulesHostBuilder builder)
        {
            builder.ConfigureFrameworkServices((_, services) =>
            {
                services.AddTransient<IAddSimpleInjectorFilter<IdentityModule>, IdentityComponentFilter>();
                services.AddTransient<IConfigureContainerFilter<IdentityModule>, IdentityComponentFilter>();
            });

            return builder;
        }

        private sealed class IdentityComponentFilter
            : IAddSimpleInjectorFilter<IdentityModule>,
                IConfigureContainerFilter<IdentityModule>
        {
            public Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    // Register as a component and advertise the identity endpoint. The identity
                    // consumes no config domain, so it has no realizers; the controller records
                    // the advertised endpoint into the Endpoints domain.
                    options.AddComponentRegistration(
                        ComponentType.Identity,
                        InboundQueue,
                        new Dictionary<string, string>
                        {
                            ["identity"] = IdentityContainerExtensions.GetIdentityUrl(),
                        });

                    next(context, options);
                };
            }

            public Action<IModuleContext<IdentityModule>, Container> Invoke(
                Action<IModuleContext<IdentityModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
                    // The identity module has no Rebus handlers of its own; the config
                    // distribution handlers are appended by AddComponentRegistration.
                    container.Collection.Register(typeof(IHandleMessages<>), typeof(IdentityModule).Assembly);

                    container.ConfigureRebus(configurer => configurer
                        .Serialization(s => s.UseEryphSettings())
                        .Transport(t =>
                            container.GetInstance<IRebusTransportConfigurer>()
                                .Configure(t, InboundQueue))
                        .Options(x => x.SetNumberOfWorkers(2))
                        .Subscriptions(s =>
                            container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
                        .Start());
                };
            }
        }
    }
}
