using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.ModuleCore.Startup;

public static class ContainerExtensions
{
    public static void AddStartupHandler<THandler>(
        this SimpleInjectorAddOptions options)
        where THandler : class, IStartupHandler
    {
        options.Container.Register<THandler>();
        options.AddHostedService<StartupHandlerService<THandler>>();
    }
}
