﻿using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.IdentityServer.EfCore.Storage.DbContexts;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.IdentityDb;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using Eryph.Runtime.Zero.Configuration.Clients;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero
{
    public static class HostIdentityModuleExtensions
    {
        public static IModulesHostBuilder AddIdentityModule(this IModulesHostBuilder builder, Container container)
        {
            builder.HostModule<IdentityModule>();

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<IdentityModule>, IdentityModuleFilters>();
                services.AddTransient<IModuleServicesFilter<IdentityModule>, IdentityModuleFilters>();
            });

            container.RegisterSingleton<IConfigReaderService<ClientConfigModel>, ClientConfigReaderService>();
            container.RegisterSingleton<IConfigWriterService<ClientConfigModel>, ClientConfigWriterService>();


            container
                .Register<IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return builder;
        }


        private class IdentityModuleFilters : IConfigureContainerFilter<IdentityModule>,
            IModuleServicesFilter<IdentityModule>
        {
            public Action<IModuleContext<IdentityModule>, Container> Invoke(
                Action<IModuleContext<IdentityModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<ClientConfigModel>>);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<ClientConfigModel>>);
                    container.RegisterDecorator(typeof(IClientService<>),
                        typeof(ClientServiceWithConfigServiceDecorator<>));

                    container.RegisterSingleton<SeedFromConfigHandler<IdentityModule>>();
                    container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityClientSeeder>();
                };
            }


            public Action<IModulesHostBuilderContext<IdentityModule>, IServiceCollection> Invoke(
                Action<IModulesHostBuilderContext<IdentityModule>, IServiceCollection> next)
            {
                return (context, services) =>
                {
                    next(context, services);
                    services.AddHostedHandler<SeedFromConfigHandler<IdentityModule>>();
                };
            }
        }
    }
}