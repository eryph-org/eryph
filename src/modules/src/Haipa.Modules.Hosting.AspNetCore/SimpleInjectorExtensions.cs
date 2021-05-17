using System;
using Microsoft.AspNetCore.Hosting;
using SimpleInjector;

namespace Haipa.Modules.Hosting
{
    public static class SimpleInjectorExtensions
    {

        public static void HostAspNetCore(this Container container, Func<string, IWebHostBuilder> builder)
        {
            container.Register(typeof(IModuleHost<>), typeof(WebModuleHost<>));

            container.RegisterInstance<IWebModuleHostBuilderFactory>(new PassThroughWebHostBuilderFactory(builder));

        }

    }
}
