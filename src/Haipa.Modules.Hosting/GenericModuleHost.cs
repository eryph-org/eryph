using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Hosting
{
    // ReSharper disable once UnusedMember.Global
    public class ModuleHost<TModule> : IModuleHost<TModule> where TModule : ModuleBase
    {
        private readonly TModule _module;
        private readonly Container _container = new Container();
        private IHost _host;

        public ModuleHost(TModule module)
        {
            _module = module;
        }

        public void Bootstrap(IServiceProvider serviceProvider)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            _host = new HostBuilder()
                .ConfigureServices(sc => ConfigureServicesAndAppContainer(serviceProvider, sc))
                .Build()
                .UseSimpleInjector(_container, options => _module.UseSimpleInjector(options));

            InvokeModuleMethod("ConfigureContainer", serviceProvider, _container);


            _container.AutoCrossWireAspNetComponents(_host.Services);
            _container.Verify();
        }

        public Task Start() => _host.StartAsync();

        public Task Stop() => _host.RunAsync();

        public Task WaitForShutdownAsync() => _host.WaitForShutdownAsync();


        private void ConfigureServicesAndAppContainer(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddSingleton(_container);
            services.EnableSimpleInjectorCrossWiring(_container);
            InvokeModuleMethod("ConfigureServices", serviceProvider, services);
            services.AddSimpleInjector(_container, _module.AddSimpleInjector);


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