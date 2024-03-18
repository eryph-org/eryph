using System;
using System.IO;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Projects;
using Eryph.Runtime.Zero.Configuration.Storage;
using Eryph.Runtime.Zero.Configuration.VMMetadata;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Runtime.Zero
{
    public static class HostControllerModuleExtensions
    {
        public static IModulesHostBuilder AddControllerModule(this IModulesHostBuilder builder, Container container)
        {
            builder.HostModule<ControllerModule>(cfg =>
            {
                cfg.Configure(app =>
                {
                    
                    using var scope = app.Services.CreateScope();
                    scope.ServiceProvider.GetRequiredService<StateStoreContext>().Database.Migrate();
                });
            });

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IModuleServicesFilter<ControllerModule>, ControllerModuleFilters>();
            });

            builder.ConfigureServices((ctc, services) =>
            {
                services.AddAutoMapper(typeof(MapperProfile).Assembly);
                services.AddSingleton<ISimpleConfigWriter<Project>>(
                    sp => ActivatorUtilities.CreateInstance<ProjectConfigWriterService>(
                        sp, Path.Combine(ZeroConfig.GetProjectsConfigPath())));
            });

            container.RegisterSingleton<IConfigReaderService<CatletMetadata>, VMMetadataConfigReaderService>();
            container.RegisterSingleton<IConfigWriterService<CatletMetadata>, VMMetadataConfigWriterService>();
            container.RegisterSingleton<IConfigReaderService<VirtualDisk>, VhdReaderService>();
            container.RegisterSingleton<IConfigWriterService<VirtualDisk>, VhdWriterService>();


            container.Register<IPlacementCalculator, ZeroAgentLocator>();
            container.Register<IStorageManagementAgentLocator, ZeroAgentLocator>();

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
                        .GetRequiredService<ISimpleConfigWriter<Project>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<CatletMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<CatletMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<VirtualDisk>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<VirtualDisk>>, Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IProjectDataService),
                        typeof(ProjectDataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IVirtualMachineMetadataService),
                        typeof(MetadataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IVirtualDiskDataService),
                        typeof(VirtualDiskDataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterSingleton<SeedFromConfigHandler<ControllerModule>>();
                    container.Collection.Append<IConfigSeeder<ControllerModule>, ProjectSeeder>();
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