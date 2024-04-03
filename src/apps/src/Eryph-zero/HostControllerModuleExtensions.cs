using System;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.ConfigModel.Networks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.Runtime.Zero.Configuration;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.Runtime.Zero.Configuration.Projects;
using Eryph.Runtime.Zero.Configuration.Storage;
using Eryph.Runtime.Zero.Configuration.VMMetadata;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.ZeroState;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
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
                    Task.Run(async () =>
                    {
                        await using var scope = app.Services.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<StateStoreContext>().Database.MigrateAsync();
                    }).Wait();
                });
            });

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IModuleServicesFilter<ControllerModule>, ControllerModuleFilters>();
                services.AddTransient<IAddSimpleInjectorFilter<ControllerModule>, ControllerModuleFilters>();
            });

            container.RegisterSingleton<IConfigReaderService<CatletMetadata>, VMMetadataConfigReaderService>();
            container.RegisterSingleton<IConfigWriterService<CatletMetadata>, VMMetadataConfigWriterService>();
            container.RegisterSingleton<IConfigReaderService<VirtualDisk>, VhdReaderService>();
            container.RegisterSingleton<IConfigWriterService<VirtualDisk>, VhdWriterService>();


            container.Register<IPlacementCalculator, ZeroAgentLocator>();
            container.Register<IStorageManagementAgentLocator, ZeroAgentLocator>();

            container.RegisterSingleton<IZeroStateConfig, ZeroStateConfig>();

            return builder;
        }


        private class ControllerModuleFilters :
            IConfigureContainerFilter<ControllerModule>,
            IModuleServicesFilter<ControllerModule>,
            IAddSimpleInjectorFilter<ControllerModule>
        {
            public Action<IModuleContext<ControllerModule>, Container> Invoke(
                Action<IModuleContext<ControllerModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<CatletMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<CatletMetadata>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<VirtualDisk>>, Lifestyle.Scoped);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<VirtualDisk>>, Lifestyle.Scoped);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IDbContextConfigurer<StateStoreContext>>, Lifestyle.Scoped);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IZeroStateConfig>, Lifestyle.Singleton);
                    
                    /*

                    container.RegisterDecorator(typeof(IVirtualMachineMetadataService),
                        typeof(MetadataServiceWithConfigServiceDecorator), Lifestyle.Scoped);

                    container.RegisterDecorator(typeof(IVirtualDiskDataService),
                        typeof(VirtualDiskDataServiceWithConfigServiceDecorator), Lifestyle.Scoped);
                    */
                    container.UseZeroState();

                    // The order of the seeders is important. The default tenant must be seeded
                    // before we try to recreate the state DB from the zero state config files.
                    container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultTenantSeeder>(Lifestyle.Scoped);
                    container.UseZeroStateSeeders();
                    container.Collection.Append<IConfigSeeder<ControllerModule>, DefaultProjectSeeder>(Lifestyle.Scoped);
                };
            }

            public Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> Invoke(
                Action<IModulesHostBuilderContext<ControllerModule>, IServiceCollection> next)
            {
                return (context, services) =>
                {
                    next(context, services);
                    //services.AddHostedHandler<SeedFromConfigHandler<ControllerModule>>();
                    /*
                    services
                        .AddSingleton<IZeroStateChannel<ZeroStateChangeSet>, ZeroStateChannel<ZeroStateChangeSet>>();
                    */
                    //services.AddHostedService<ZeroStateBackgroundService>();
                };
            }

            public Action<IModulesHostBuilderContext<ControllerModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<ControllerModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    options.AddZeroStateService();
                    options.AddHostedService<SeedFromConfigHandler<ControllerModule>>();
                    next(context, options);
                };
            }
        }
    }
}