using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder AsModuleHost<TModule>(this IWebHostBuilder builder, ModuleHostContext<TModule> hostContext) 
            where TModule: WebModuleBase
        {
            builder.ConfigureServices(sc => ConfigureServices(hostContext, sc))
                .UseContentRoot(@"..\..\..\..\..\src\" + hostContext.Module.Name)
                .Configure(app => ConfigureAppAndContainer(app, hostContext));

            return builder;
        }

        private static void ConfigureServices<TModule>(ModuleHostContext<TModule> hostContext, IServiceCollection services)
            where TModule : WebModuleBase
        {
            services.AddSingleton(hostContext.Container);
            services.EnableSimpleInjectorCrossWiring(hostContext.Container);
            hostContext.Module.ConfigureServices(hostContext.ModuleServiceProvider, services);
            services.AddSimpleInjector(hostContext.Container, hostContext.Module.AddSimpleInjector);


        }

        private static void ConfigureAppAndContainer<TModule>(IApplicationBuilder app, ModuleHostContext<TModule> hostContext)
            where TModule : WebModuleBase

        {
            hostContext.Module.Configure(app);
            app.UseSimpleInjector(hostContext.Container, hostContext.Module.UseSimpleInjector);
            hostContext.Module.ConfigureContainer(hostContext.ModuleServiceProvider, hostContext.Container);

            hostContext.Container.AutoCrossWireAspNetComponents(app);
            hostContext.Container.Verify();

        }
    }
}