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

        public ModuleHostBuilder(Container container)
        {
            Container = container;
        }

        public Container Container { get; }

        public IModuleHostBuilder AddModule<TModule>() where TModule : class, IModule
        {

            Container.RegisterSingleton<TModule>();
            Container.Collection.Append<IModule, TModule>(Lifestyle.Singleton);
            return this;
        }

        public async Task RunModule<TModule>(Func<IServiceProvider, Task> prepareStartFunc = null) where TModule : class, IModule
        {
            var module = Container.GetInstance<TModule>();
            var host = Container.CreateModuleHost(module);
            host.Bootstrap(Container);

            await host.Start().ConfigureAwait(false);
            await host.WaitForShutdownAsync().ConfigureAwait(false);
            await host.Stop().ConfigureAwait(false);

        }

    }

    public interface IModuleHostBuilder
    {
        Container Container { get; }
        IModuleHostBuilder AddModule<TModule>() where TModule : class, IModule;
        Task RunModule<TModule>(Func<IServiceProvider, Task> prepareStartFunc = null) where TModule : class, IModule;
    }
}
