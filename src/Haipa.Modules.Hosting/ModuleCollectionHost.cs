using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public class ModuleCollectionHost
    {
        private readonly IEnumerable<IModule> _modules;
        private readonly List<IModuleHost> _moduleHosts = new List<IModuleHost>();
        private readonly Container _container;

        public ModuleCollectionHost(IEnumerable<IModule> modules, Container container)
        {
            _modules = modules;
            _container = container;
        }

        public bool Start()
        {

            foreach (var module in _modules)
            {
                var host = _container.CreateModuleHost(module);
                _moduleHosts.Add(host);

            }

            _moduleHosts.AsParallel().ForAll(host=> host.Bootstrap(_container));

            foreach (var host in _moduleHosts)
            {
                host.Start().GetAwaiter().GetResult();
            }

            return true;
        }

        public bool Stop()
        {
            foreach (var host in _moduleHosts)
            {
                host.Stop().GetAwaiter().GetResult();
            }

            return true;
        }
    }
}