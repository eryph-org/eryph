using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.IdentityDb;
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

            container.RegisterSingleton<IConfigReaderService<ClientConfigModel>, ClientConfigReaderService>();
            container.RegisterSingleton<IConfigWriterService<ClientConfigModel>, ClientConfigWriterService>();
            container.Register<ISigningCertificateManager, SigningCertificateManager>();

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

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigWriterService<ClientConfigModel>>);
                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IConfigReaderService<ClientConfigModel>>);
                    container.RegisterDecorator(typeof(IClientService),
                        typeof(ClientServiceWithConfigServiceDecorator));

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