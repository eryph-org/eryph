using System;
using Haipa.IdentityDb;
using Haipa.Modules;
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Services;
using Haipa.Runtime.Zero.ConfigStore;
using Haipa.Runtime.Zero.ConfigStore.Clients;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Haipa.Runtime.Zero
{
    public static class HostIdentityModuleExtensions
    {
        public static IModuleHostBuilder AddIdentityModule(this IModuleHostBuilder builder)
        {
            builder.AddModule<IdentityModule>();

            builder.Container.RegisterSingleton<IConfigReaderService<ClientConfigModel>, ClientConfigReaderService>();
            builder.Container.RegisterSingleton<IConfigWriterService<ClientConfigModel>, ClientConfigWriterService>();
            builder.Container.Register<IContainerConfigurer<IdentityModule>, IdentityModuleContainerConfigurer>();
            builder.Container.Register<IServicesConfigurer<IdentityModule>, IdentityModuleServicesConfigurer>();

            builder.Container.Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return builder;
        }


        private class IdentityModuleContainerConfigurer : IContainerConfigurer<IdentityModule>
        {
            public void ConfigureContainer(IdentityModule module, IServiceProvider serviceProvider, Container container)
            {
                container.Register(serviceProvider.GetRequiredService<IConfigWriterService<ClientConfigModel>>);
                container.Register(serviceProvider.GetRequiredService<IConfigReaderService<ClientConfigModel>>);
                container.RegisterDecorator(typeof(IClientService<>), typeof(ClientServiceWithConfigServiceDecorator<>));
                container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityClientSeeder>();

            }
        }

        private class IdentityModuleServicesConfigurer : IServicesConfigurer<IdentityModule>
        {
            public void ConfigureServices(IdentityModule module, IServiceProvider serviceProvider, IServiceCollection services)
            {
                services.AddScopedModuleHandler<SeedFromConfigHandler<IdentityModule>>();
            }
        }
    }
}