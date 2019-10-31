using System;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public class ModuleHostContext<TModule> where TModule : IModule
    {
        public ModuleHostContext(TModule module, Container container, IServiceProvider moduleServiceProvider)
        {
            Module = module;
            Container = container;
            ModuleServiceProvider = moduleServiceProvider;
        }

        public TModule Module { get; }
        public Container Container { get; }
        public IServiceProvider ModuleServiceProvider { get; }

        

    }
}