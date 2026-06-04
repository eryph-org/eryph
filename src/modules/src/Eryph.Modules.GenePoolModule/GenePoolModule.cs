using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net.Http;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.GenePool.Genetics;
using Eryph.Modules.GenePool.Inventory;
using Eryph.Rebus;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Subscriptions;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
public class GenePoolModule
{
    public string Name => "Eryph.GenePoolModule";

    [UsedImplicitly]
    public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
    {
        services.Configure<HostOptions>(
            opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

        services.AddHttpClient(GenePoolConstants.PartClientName)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
            .AddPolicyHandler(GetRetryPolicy());
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<GeneticsRequestWatcherService>();
        options.AddStartupHandler<StartBusModuleHandler>();

        // Opt in to controller-driven component registration so the controller can route gene
        // operations to this component's inbound queue (and track its liveness). GenePool consumes
        // no distributed config domains, so it registers no realizers — it registers only to be
        // discoverable and routable. The inbound queue must equal the bus endpoint configured below.
        options.AddComponentRegistration(
            ComponentType.GenePoolAgent,
            $"{QueueNames.GenePool}.{Environment.MachineName}",
            new Dictionary<string, string>());

        options.AddLogging();
    }

    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.RegisterSingleton<IFileSystem, FileSystem>();
        container.RegisterSingleton<IFileSystemService, FileSystemService>();

        container.RegisterInstance(serviceProvider.GetRequiredService<IApplicationInfoProvider>());
        container.RegisterInstance(serviceProvider.GetRequiredService<IGenePoolApiKeyStore>());
        container.RegisterInstance(serviceProvider.GetRequiredService<IGenePoolPathProvider>());
        container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
       // container.RegisterSingleton<IHostInfoProvider, HostInfoProvider>();
       // container.RegisterSingleton<IHardwareIdProvider, HardwareIdProvider>();
       // container.RegisterSingleton<IHostArchitectureProvider, HostArchitectureProvider>();
       
       if (OperatingSystem.IsWindows())
       {
           container.RegisterSingleton<IHardwareIdProvider, WindowsHardwareIdProvider>();
        }

        var genePoolFactory = new GenePoolFactory(container);
        
        genePoolFactory.Register<RepositoryGenePool>(serviceProvider.GetRequiredService<GenePoolSettings>());
        container.RegisterInstance<IGenePoolFactory>(genePoolFactory);
        container.RegisterSingleton<IGeneRequestRegistry, GeneRequestRegistry>();
        container.Register<IGenePoolInventory, GenePoolInventory>(Lifestyle.Scoped);
        container.Register<IGeneProvider, LocalFirstGeneProvider>(Lifestyle.Scoped);
        container.Register<ILocalGenePool, LocalGenePoolSource>(Lifestyle.Scoped);
        container.Register<IGenePoolReader, GenePoolReader>(Lifestyle.Scoped);

        container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
        container.Collection.Register(typeof(IHandleMessages<>), typeof(GenePoolModule).Assembly);
        container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationTaskHandler<>), Lifestyle.Scoped);
        container.AddRebusOperationsHandlers();

        container.ConfigureRebus(configurer => configurer
            .Serialization(s => s.UseEryphSettings())
            // Use the registered component inbound queue as the single source of truth for the bus
            // endpoint name (it must match what AddComponentRegistration announced). Resolved inside
            // the transport lambda (bus start) so it does not trigger premature container
            // verification during ConfigureContainer.
            .Transport(t =>
                container.GetService<IRebusTransportConfigurer>()
                    .Configure(t, container.GetInstance<ComponentIdentity>().InboundQueue))
            .Options(x =>
            {
                x.RetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength:5);
                x.SetNumberOfWorkers(5);
                x.EnableSynchronousRequestReply();
            })
            .Subscriptions(s => container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            TimeSpan.FromSeconds(1), 5);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<HttpRequestException>(ex => true)
            .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                retryAttempt)));
    }
}