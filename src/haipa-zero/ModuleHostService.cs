using System.Collections.Generic;
using Haipa.Modules.Abstractions;

namespace Haipa.Runtime.Zero
{
    public class ModuleHostService
    {
        private readonly IEnumerable<IModule> _modules;

        public ModuleHostService(IEnumerable<IModule> modules)
        {
            _modules = modules;
        }

        public bool Start()
        {
            foreach (var module in _modules)
            {
                module.Start();
            }

            return true;
        }

        public bool Stop()
        {
            foreach (var module in _modules)
            {
                module.Stop();
            }

            return true;
        }
    }
}