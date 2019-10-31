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

        public abstract void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services);


        public virtual void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

        }


    }
}
