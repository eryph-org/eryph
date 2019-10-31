using System;
using System.Reflection;
using System.Threading.Tasks;
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
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

        }

        public void Bootstrap(IServiceProvider serviceProvider)
        {
            var hostContext = new ModuleHostContext<TModule>(_module, _container, serviceProvider);

            _host = new HostBuilder().CreateModuleHost(hostContext);
        }

        public Task Start() => _host.StartAsync();

        public Task Stop() => _host.RunAsync();

        public Task WaitForShutdownAsync() => _host.WaitForShutdownAsync();


    }
}