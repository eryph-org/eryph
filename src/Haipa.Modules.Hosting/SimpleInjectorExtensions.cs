using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public static class SimpleInjectorExtensions
    {

        public static void HostAspNetCore(this Container container, Func<string, IWebHostBuilder> builder)
        {
            container.RegisterInstance<IWebModuleHostBuilderFactory>(new PassThroughWebHostBuilderFactory(builder));

        }

        public static void HostModule<TModule>(this Container container) where TModule : class, IModule
        {
            container.RegisterSingleton<TModule>();
            container.Collection.Append<IModule, TModule>(Lifestyle.Singleton);
        }

        public static async Task RunModule<TModule>(this Container container) where TModule: class, IModule
        {
            container.GetInstance<TModule>().Bootstrap(container);
            await container.GetInstance<TModule>().Start().ConfigureAwait(false);
            await container.GetInstance<TModule>().WaitForShutdownAsync().ConfigureAwait(false);
            await container.GetInstance<TModule>().Stop().ConfigureAwait(false);

        }

}
}
