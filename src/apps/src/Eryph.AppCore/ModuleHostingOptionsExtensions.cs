using System;
using Dbosoft.Hosuto.Modules.Hosting;
using SimpleInjector;

namespace Eryph;

public static class ModuleHostingOptionsExtensions
{
    public static IModuleHostingOptions ConfigureContainer(this IModuleHostingOptions options, Action<Container> configure)
    {
        options.Configure(ctx =>
        {
            var _ = ctx.Advanced.RootContext;
            if (ctx is ISimpleInjectorModuleContext simpleInjectorModuleContext)
            {
                configure(simpleInjectorModuleContext.Container);
            }
        });
        return options;
    }
}