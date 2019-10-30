using System;
using System.Threading.Tasks;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public static class SimpleInjectorExtensions
    {

        public static IModuleHostBuilder HostModules(this Container container)
        {

            return new ModuleHostBuilder(container);
        }


        internal static IModuleHost CreateModuleHost(this Container container, IModule module)
        {
            var hostType = typeof(IModuleHost<>).MakeGenericType(module.GetType());
            return (IModuleHost) container.GetInstance(hostType);

        }


    }

    public class ModuleHostBuilder : IModuleHostBuilder
    {
        private readonly Container _container;

        public ModuleHostBuilder(Container container)
        {
            _container = container;
        }

        public IModuleHostBuilder AddModule<TModule>() where TModule : class, IModule
        {

            _container.RegisterSingleton<TModule>();
            _container.Collection.Append<IModule, TModule>(Lifestyle.Singleton);
            return this;
        }

        public async Task RunModule<TModule>(Func<IServiceProvider, Task> prepareStartFunc = null) where TModule : class, IModule
        {
            var module = _container.GetInstance<TModule>();
            var host = _container.CreateModuleHost(module);
            host.Bootstrap(_container);

            await host.Start().ConfigureAwait(false);
            await host.WaitForShutdownAsync().ConfigureAwait(false);
            await host.Stop().ConfigureAwait(false);

        }

    }

    public interface IModuleHostBuilder
    {
        IModuleHostBuilder AddModule<TModule>() where TModule : class, IModule;
        Task RunModule<TModule>(Func<IServiceProvider, Task> prepareStartFunc = null) where TModule : class, IModule;
    }
}
