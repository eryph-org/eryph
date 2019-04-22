using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Haipa.Modules
{
    public static class ModuleServiceCollectionExtensions
    {
        public static IServiceCollection AddModuleService<THostedService>(
            this IServiceCollection services)
            where THostedService : class, IHostedService
        {
            return services.AddSingleton<IHostedService, ModuleHostedService<THostedService>>();
        }

        public static IServiceCollection AddModuleHandler<TModuleHandler>(
            this IServiceCollection services)
            where TModuleHandler : class, IModuleHandler
        {
            return services.AddSingleton<IHostedService, ModuleHostedHandler<TModuleHandler>>();
        }


        public static IServiceCollection AddScopedModuleHandler<TModuleHandler>(
            this IServiceCollection services)
            where TModuleHandler : class, IModuleHandler
        {
            return services.AddSingleton<IHostedService, ScopedModuleHandler<TModuleHandler>>();
        }
    }
}