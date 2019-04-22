using System.Collections.Generic;
using System.Linq;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public class ModuleHost
    {
        private readonly IEnumerable<IModule> _modules;
        private readonly Container _container;

        public ModuleHost(IEnumerable<IModule> modules, Container container)
        {
            _modules = modules;
            _container = container;
        }

        public bool Start()
        {
            _modules.AsParallel().ForAll((m => m.Bootstrap(_container)));

            foreach (var module in _modules)
            {
                module.Start().GetAwaiter().GetResult();
            }

            return true;
        }

        public bool Stop()
        {
            foreach (var module in _modules)
            {
                module.Stop().GetAwaiter().GetResult();
            }

            return true;
        }
    }
}