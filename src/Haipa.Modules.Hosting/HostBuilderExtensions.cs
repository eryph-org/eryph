using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder AsModuleHost<TModule>(this IHostBuilder builder, ModuleHostContext<TModule> hostContext)
            where TModule : ModuleBase
        {
            builder.ConfigureServices(sc => ConfigureServicesAndAppContainer(hostContext, sc));
            return builder;
        }

        public static IHost CreateModuleHost<TModule>(this IHostBuilder builder, ModuleHostContext<TModule> hostContext, Action<IHostBuilder> configureBuilder = null, Action<Container> configureContainer = null)
            where TModule : ModuleBase
        {

            builder.AsModuleHost(hostContext);

            configureBuilder?.Invoke(builder);

            var host = builder.Build()
                .UseSimpleInjector(hostContext.Container, options => hostContext.Module.UseSimpleInjector(options));
           
            hostContext.Module.ConfigureContainer(hostContext.ModuleServiceProvider, hostContext.Container);

            var configurer = hostContext.ModuleServiceProvider.GetService<IContainerConfigurer<TModule>>();
            configurer?.ConfigureContainer(hostContext.Module, hostContext.ModuleServiceProvider, hostContext.Container);
            
            configureContainer?.Invoke(hostContext.Container);

            hostContext.Container.AutoCrossWireAspNetComponents(host.Services);
            hostContext.Container.Verify();
            
            return host;

        }

        private static void ConfigureServicesAndAppContainer<TModule>(ModuleHostContext<TModule> hostContext, IServiceCollection services)
            where TModule : ModuleBase
        {
            services.AddSingleton(hostContext.Container);
            services.EnableSimpleInjectorCrossWiring(hostContext.Container);
            hostContext.Module.ConfigureServices(hostContext.ModuleServiceProvider, services);

            var configurer = hostContext.ModuleServiceProvider.GetService<IServicesConfigurer<TModule>>();
            configurer?.ConfigureServices(hostContext.Module, hostContext.ModuleServiceProvider, services);


            services.AddSimpleInjector(hostContext.Container, hostContext.Module.AddSimpleInjector);

        }

    }
}