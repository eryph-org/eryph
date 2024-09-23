using System;
using System.IO.Abstractions;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.IdentityDb;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller;
using Eryph.Modules.Identity;
using Eryph.Modules.Identity.Services;
using Eryph.Runtime.Zero.Configuration.Clients;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

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
                services.AddTransient<IAddSimpleInjectorFilter<IdentityModule>, IdentityModuleFilters>();
            });

            container.Register<ITokenCertificateManager, TokenCertificateManager>();

            container
                .Register<IDbContextConfigurer<IdentityDbContext>, InMemoryIdentityDbContextConfigurer>();

            return builder;
        }


        private class IdentityModuleFilters : IConfigureContainerFilter<IdentityModule>,
            IAddSimpleInjectorFilter<IdentityModule>
        {
            public Action<IModuleContext<IdentityModule>, Container> Invoke(
                Action<IModuleContext<IdentityModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.RegisterSingleton<IClientConfigService, ClientConfigService>();
                    container.RegisterDecorator(typeof(IClientService),
                        typeof(ClientServiceWithConfigServiceDecorator));

                    container.RegisterSingleton<IFileSystem, FileSystem>();
                    container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityClientSeeder>();
                    container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityScopesSeeder>();
                };
            }

            public Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> Invoke(
                Action<IModulesHostBuilderContext<IdentityModule>, SimpleInjectorAddOptions> next)
            {
                return (context, options) =>
                {
                    options.AddHostedService<SeedFromConfigHandler<IdentityModule>>();
                    next(context, options);
                };
            }
        }
    }
}