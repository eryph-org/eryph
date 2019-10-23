using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

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

        public static async Task RunModule<TModule>(this Container container, Func<IServiceProvider,Task> prepareStartFunc = null) where TModule: class, IModule
        {
            var module = container.GetInstance<TModule>();
                
            var sp = module.Bootstrap(container);
            if (prepareStartFunc != null)
            {
                await prepareStartFunc(sp).ConfigureAwait(false);
            }

            await module.Start().ConfigureAwait(false);
            await module.WaitForShutdownAsync().ConfigureAwait(false);
            await module.Stop().ConfigureAwait(false);

        }

}
}
