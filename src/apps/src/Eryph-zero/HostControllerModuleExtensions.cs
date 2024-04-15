using System;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Runtime.Zero
{
    public static class HostControllerModuleExtensions
    {
        public static IModulesHostBuilder AddControllerModule(this IModulesHostBuilder builder, Container container)
        {
            builder.HostModule<ControllerModule>(cfg =>
            {
                cfg.Configure(app =>
                {
                    Task.Run(async () =>
                    {
                        await using var scope = app.Services.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<StateStoreContext>().Database.MigrateAsync();
                    }).Wait();
                });
            });

            builder.ConfigureFrameworkServices((ctx, services) =>
            {
                services.AddTransient<IConfigureContainerFilter<ControllerModule>, ControllerModuleFilters>();
            });

            container.Register<IPlacementCalculator, ZeroAgentLocator>();
            container.Register<IStorageManagementAgentLocator, ZeroAgentLocator>();

            return builder;
        }


        private class ControllerModuleFilters :
            IConfigureContainerFilter<ControllerModule>
        {
            public Action<IModuleContext<ControllerModule>, Container> Invoke(
                Action<IModuleContext<ControllerModule>, Container> next)
            {
                return (context, container) =>
                {
                    next(context, container);

                    container.Register(context.ModulesHostServices
                        .GetRequiredService<IDbContextConfigurer<StateStoreContext>>, Lifestyle.Scoped);
                };
            }
        }
    }
}