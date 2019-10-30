using System;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Container = SimpleInjector.Container;

namespace Haipa.Modules
{

    public abstract class ModuleBase : IModule
    {
        public abstract string Name { get; }

        public virtual void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
          
        }

        public virtual void UseSimpleInjector(SimpleInjectorUseOptions options)
        {

        }

        protected abstract void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services);


        protected virtual void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

        }


    }
}
