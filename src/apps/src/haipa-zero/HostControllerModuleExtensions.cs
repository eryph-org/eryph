using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Configuration;
using Haipa.Configuration.Model;
using Haipa.IdentityDb;
using Haipa.Modules.Controller;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Services;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using Haipa.Runtime.Zero.Configuration;
using Haipa.Runtime.Zero.Configuration.Clients;
using Haipa.Runtime.Zero.Configuration.VMMetadata;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Haipa.Runtime.Zero
{
    public static class HostControllerModuleExtensions
    {
        public static IModulesHostBuilder AddControllerModule(this IModulesHostBuilder builder, Container container)
        {
            builder.HostModule<ControllerModule>();

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IModuleServicesFilter<ControllerModule>, ControllerModuleFilters>();
            });

            container.RegisterSingleton<IConfigReaderService<VirtualMachineMetadata>, VMMetadataConfigReaderService>();
            container.RegisterSingleton<IConfigWriterService<VirtualMachineMetadata>, VMMetadataConfigWriterService>();


            container.Register<IPlacementCalculator, ZeroAgentPlacementCalculator>();

            return builder;
        }


        private class ControllerModuleFilters : IConfigureContainerFilter<ControllerModule>, IModuleServicesFilter<ControllerModule>
        {
            public Action<IModuleContext<ControllerModule>, Container> Invoke(Action<IModuleContext<ControllerModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<VirtualMachineMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<VirtualMachineMetadata>>, Lifestyle.Scoped);
                    container.RegisterDecorator(typeof(IVirtualMachineMetadataService),
                        typeof(MetadataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterSingleton<SeedFromConfigHandler<ControllerModule>>();
                    container.Collection.Append<IConfigSeeder<ControllerModule>, VMMetadataSeeder>();

                };
            }


            public Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> Invoke(Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> next)
            {
                return (context, services) =>
                {
                    next(context, services);
                    services.AddHostedHandler<SeedFromConfigHandler<ControllerModule>>();

                };
            }
        }

    }
}