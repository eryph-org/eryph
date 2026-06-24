using System;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.GenePool;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.GenePool;

/// <summary>
/// Hosts the gene pool module as a standalone runtime. Mirrors eryph-zero's gene pool host
/// extension but uses the component mTLS / RabbitMQ transport (instead of the in-process
/// in-memory bus) so it talks to a separately running controller and downloads genes on its
/// own per-host queue.
/// </summary>
internal static class HostGenePoolModuleExtensions
{
    public static IModulesHostBuilder AddGenePoolModule(
        this IModulesHostBuilder builder)
    {
        builder.HostModule<GenePoolModule>();
        builder.ConfigureFrameworkServices((_, services) =>
        {
            services.AddTransient<IAddSimpleInjectorFilter<GenePoolModule>, GenePoolModuleFilter>();
            services.AddTransient<IConfigureContainerFilter<GenePoolModule>, GenePoolModuleFilter>();
        });

        return builder;
    }

    private sealed class GenePoolModuleFilter
        : IAddSimpleInjectorFilter<GenePoolModule>,
            IConfigureContainerFilter<GenePoolModule>
    {
        public Action<IModulesHostBuilderContext<GenePoolModule>, SimpleInjectorAddOptions> Invoke(
            Action<IModulesHostBuilderContext<GenePoolModule>, SimpleInjectorAddOptions> next)
        {
            return (context, options) =>
            {
                // Renew the component certificate before it expires without a restart (the context
                // is registered by ComponentMtlsTransport.Register in ConfigureContainer below).
                ComponentMtlsTransport.AddRenewal(options);
                next(context, options);
            };
        }

        public Action<IModuleContext<GenePoolModule>, Container> Invoke(
            Action<IModuleContext<GenePoolModule>, Container> next)
        {
            return (context, container) =>
            {
                // The module configures and starts its bus inside ConfigureContainer (run by
                // next), so the transport must be registered first.
                ComponentMtlsTransport.Register(
                    container,
                    context.ModulesHostServices.GetRequiredService<IConfiguration>(),
                    context.ModulesHostServices.GetRequiredService<ILoggerFactory>(),
                    ComponentType.GenePoolAgent);

                next(context, container);
            };
        }
    }
}
