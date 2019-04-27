using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;

namespace Haipa.Modules
{
    public abstract class WebModuleBase : ModuleBase
    {
        public abstract string Path { get; }
        private Container _container;
        private IServiceProvider _moduleServiceProvider;

        public sealed override void Bootstrap(IServiceProvider serviceProvider)
        {
            ConfigureContainerWithServicesAction = EmptyAction;

            Host = new WebHostAsHost(
                serviceProvider.GetRequiredService<IWebModuleHostBuilderFactory>().CreateWebHostBuilder(Name, Path)
                .ConfigureServices(sc => ConfigureServicesAndAppContainer(serviceProvider, sc))
                .UseContentRoot(@"..\..\..\..\" + Name)
                .Configure(ConfigureAppAndContainer)           
                .Build());

        }

        protected sealed override void WireUpServicesAndContainer(IServiceProvider serviceProvider, IServiceCollection services, Container container)
        {
            _container = container;
            _moduleServiceProvider = serviceProvider;

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(container);

            services.AddSingleton<IControllerActivator>(
                new SimpleInjectorControllerActivator(container));
            services.AddSingleton<IViewComponentActivator>(
                new SimpleInjectorViewComponentActivator(container));


            services.UseSimpleInjectorAspNetRequestScoping(container);

        }

        private void ConfigureAppAndContainer(IApplicationBuilder app)
        {
            _container.RegisterMvcControllers(app);
            _container.RegisterMvcViewComponents(app);

            Configure(app);
            ConfigureContainer(_moduleServiceProvider, _container);
            _container.AutoCrossWireAspNetComponents(app);
            _container.Verify();

        }


        protected abstract void Configure(IApplicationBuilder app);

        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            
        }
    }
}