using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Haipa.Configuration;
using Haipa.Modules.Controller;
using Haipa.Modules.Controller.DataServices;
using Haipa.Runtime.Zero.Configuration.Storage;
using Haipa.Runtime.Zero.Configuration.VMMetadata;
using Haipa.StateDb.Model;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using VirtualMachineMetadata = Haipa.Resources.Machines.VirtualMachineMetadata;

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
            container.RegisterSingleton<IConfigReaderService<VirtualDisk>, VhdReaderService>();
            container.RegisterSingleton<IConfigWriterService<VirtualDisk>, VhdWriterService>();


            container.Register<IPlacementCalculator, ZeroAgentPlacementCalculator>();

            return builder;
        }


        private class ControllerModuleFilters : IConfigureContainerFilter<ControllerModule>,
            IModuleServicesFilter<ControllerModule>
        {
            public Action<IModuleContext<ControllerModule>, Container> Invoke(
                Action<IModuleContext<ControllerModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<VirtualMachineMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<VirtualMachineMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<VirtualDisk>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<VirtualDisk>>, Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IVirtualMachineMetadataService),
                        typeof(MetadataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IVirtualDiskDataService),
                        typeof(VirtualDiskDataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterSingleton<SeedFromConfigHandler<ControllerModule>>();
                    container.Collection.Append<IConfigSeeder<ControllerModule>, VMMetadataSeeder>();
                    container.Collection.Append<IConfigSeeder<ControllerModule>, VirtualDiskSeeder>();

                };
            }


            public Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> Invoke(
                Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> next)
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