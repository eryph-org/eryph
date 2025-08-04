using System;
using System.IO.Abstractions;
using System.Net.Http;
using System.Runtime.InteropServices;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.GenePool.Genetics;
using Eryph.Modules.GenePool.Inventory;
using Eryph.Rebus;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
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

namespace Eryph.Modules.GenePool
{
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
            options.AddLogging();
        }

        [UsedImplicitly]
        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.RegisterSingleton<IFileSystem, FileSystem>();
            container.RegisterSingleton<IFileSystemService, FileSystemService>();


            container.RegisterInstance(serviceProvider.GetRequiredService<IVmHostAgentConfigurationManager>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IApplicationInfoProvider>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IGenePoolApiKeyStore>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IHostSettingsProvider>());
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
            container.RegisterSingleton<IGeneProvider, LocalFirstGeneProvider>();
            container.RegisterSingleton<IGeneRequestDispatcher, GeneRequestRegistry>();
            container.RegisterSingleton<IGeneRequestBackgroundQueue, GeneBackgroundTaskQueue>();
            container.RegisterSingleton<IGenePoolInventoryFactory, GenePoolInventoryFactory>();


            container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
            container.Collection.Register(typeof(IHandleMessages<>), typeof(GenePoolModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationTaskHandler<>), Lifestyle.Scoped);
            container.AddRebusOperationsHandlers();

            var localName = $"{QueueNames.GenePool}.{Environment.MachineName}";
            container.ConfigureRebus(configurer => configurer
                .Serialization(s => s.UseEryphSettings())
                .Transport(t =>
                    container.GetService<IRebusTransportConfigurer>()
                        .Configure(t, localName))
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
}