using System;
using System.Threading.Tasks;
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

        public WebModuleHost(TModule module)
        {
            _module = module;
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        }

        public void Bootstrap(IServiceProvider serviceProvider)
        {
            var hostContext = new ModuleHostContext<TModule>(_module, _container, serviceProvider);

            _host = serviceProvider.GetRequiredService<IWebModuleHostBuilderFactory>().CreateWebHostBuilder(_module.Name, _module.Path)
                .AsModuleHost(hostContext)
                .Build();

        }

        public Task Start() => _host.StartAsync();

        public Task Stop() => _host.RunAsync();

        public Task WaitForShutdownAsync() => _host.WaitForShutdownAsync();


    }
}