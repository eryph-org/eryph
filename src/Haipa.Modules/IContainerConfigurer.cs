using System;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Haipa.Modules
{
    public interface IContainerConfigurer<TModule> where TModule : IModule
    {
        void ConfigureContainer(TModule module, IServiceProvider serviceProvider, Container container);
    }

    public interface IServicesConfigurer<TModule> where TModule : IModule
    {
        void ConfigureServices(TModule module, IServiceProvider serviceProvider, IServiceCollection services);
    }
}