using System;
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
            container.Collection.Append<IModule, TModule>(Lifestyle.Singleton);
        }

    }
}
