using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Hosting
{
    public class WebModuleHost<TModule> : IModuleHost<TModule> where TModule : WebModuleBase
    {
        private readonly TModule _module;
        private readonly Container _container = new Container();
        private IWebHost _host;
        private IServiceProvider _moduleServiceProvider;

        public WebModuleHost(TModule module)
        {
            _module = module;
        }

        public void Bootstrap(IServiceProvider serviceProvider)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            _host = serviceProvider.GetRequiredService<IWebModuleHostBuilderFactory>().CreateWebHostBuilder(_module.Name, _module.Path)
                .ConfigureServices(sc => ConfigureServicesAndAppContainer(serviceProvider, sc))
                .UseContentRoot(@"..\..\..\..\" + _module.Name)
                .Configure(ConfigureAppAndContainer)
                .Build();

        }

        public Task Start() => _host.StartAsync();

        public Task Stop() => _host.RunAsync();

        public Task WaitForShutdownAsync() => _host.WaitForShutdownAsync();


        private void ConfigureServicesAndAppContainer(IServiceProvider serviceProvider, IServiceCollection services)
        {
            _moduleServiceProvider = serviceProvider;
            services.AddSingleton(_container);
            services.EnableSimpleInjectorCrossWiring(_container);
            InvokeModuleMethod("ConfigureServices", serviceProvider, services);
            services.AddSimpleInjector(_container, _module.AddSimpleInjector);


        }

        private void ConfigureAppAndContainer(IApplicationBuilder app)
        {
            InvokeModuleMethod("Configure", app);
            app.UseSimpleInjector(_container, _module.UseSimpleInjector);
            InvokeModuleMethod("ConfigureContainer", _moduleServiceProvider, _container);

            _container.AutoCrossWireAspNetComponents(app);
            _container.Verify();

        }

        public void InvokeModuleMethod(string methodName, params object[] args)
        {
            var type = typeof(TModule);
            var method = type.GetTypeInfo().GetDeclaredMethod(methodName);

            if (method == null)
                return;

            method.Invoke(_module, args);
        }
    }
}