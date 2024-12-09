using System;
using System.IO.Abstractions;
using System.Net.Http;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.OVN.Windows;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Networks;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Tracing;
using Eryph.Rebus;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Quartz;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Subscriptions;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VmHostAgentModule
    {
        private readonly TracingConfig _tracingConfig = new();

        public VmHostAgentModule(IConfiguration configuration)
        {
            configuration.GetSection("Tracing")
                .Bind(_tracingConfig);
        }

        public string Name => "Eryph.VmHostAgent";

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.Configure<HostOptions>(
                opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));

            services.AddHttpClient(GenePoolConstants.PartClientName)
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
                .AddPolicyHandler(GetRetryPolicy());

            services.AddQuartz(q =>
            {
                q.AddJob<WmiVmUptimeCheckJob>(
                    j => j.WithIdentity("uptime-check-job")
                        .DisallowConcurrentExecution());

                q.AddTrigger(t =>
                {
                    t.WithIdentity("uptime-check-job-trigger")
                        .ForJob("uptime-check-job")
                        .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever())
                        .StartNow();
                });
            });
            services.AddQuartzHostedService();
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddHostedService<SyncService>();
            options.AddHostedService<OVSChassisService>();
            options.AddHostedService<GeneticsRequestWatcherService>();
            options.AddStartupHandler<StartBusModuleHandler>();
            // Remove this hosted service to avoid triggering the inventory
            // based on WMI events.
            options.AddHostedService<VmChangeWatcherService>();
            options.AddHostedService<VmStateChangeWatcherService>();
            options.AddLogging();
        }

        [UsedImplicitly]
        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<ISyncClient, SyncClient>();
            container.Register<IHostNetworkCommands<AgentRuntime>, HostNetworkCommands<AgentRuntime>>();
            container.Register<IOVSControl, OVSControl>();
            container.RegisterInstance(serviceProvider.GetRequiredService<INetworkSyncService>());

            container.RegisterSingleton<OVNChassisNode>();
            container.RegisterSingleton<OVSDbNode>();
            container.RegisterSingleton<OVSSwitchNode>();
            container.RegisterSingleton<IOVSService<OVNChassisNode>, OVSNodeService<OVNChassisNode>>();
            container.RegisterSingleton<IOVSService<OVSDbNode>, OVSNodeService<OVSDbNode>>();
            container.RegisterSingleton<IOVSService<OVSSwitchNode>, OVSNodeService<OVSSwitchNode>>();

            container.RegisterSingleton<IFileSystem, FileSystem>();
            container.RegisterSingleton<IFileSystemService, FileSystemService>();
            container.RegisterInstance(serviceProvider.GetRequiredService<IAgentControlService>());

            if (_tracingConfig.Enabled)
            {
                container.RegisterSingleton<ITracer, Tracer>();
                container.RegisterSingleton<ITraceWriter, DiagnosticTraceWriter>();
                container.RegisterDecorator(typeof(IHandleMessages<>), typeof(TraceDecorator<>));
            }

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();

            container.Register<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>(Lifestyle.Scoped);
            container.RegisterInstance(serviceProvider.GetRequiredService<IVmHostAgentConfigurationManager>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IApplicationInfoProvider>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IGenePoolApiKeyStore>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IHostSettingsProvider>());
            container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
            container.RegisterSingleton<IHostInfoProvider, HostInfoProvider>();
            container.RegisterSingleton<IHardwareIdProvider, HardwareIdProvider>();
            container.RegisterSingleton<IHostArchitectureProvider, HostArchitectureProvider>();

            container.Register<IOVSPortManager, OvsPortManager>(Lifestyle.Scoped);
            container.Register<IHyperVOvsPortManager>(() => new HyperVOvsPortManager(), Lifestyle.Scoped);


            var genePoolFactory = new GenePoolFactory(container);
            
            genePoolFactory.Register<RepositoryGenePool>(serviceProvider.GetRequiredService<GenepoolSettings>());
            container.RegisterInstance<IGenePoolFactory>(genePoolFactory);
            container.RegisterSingleton<IGeneProvider, LocalFirstGeneProvider>();
            container.RegisterSingleton<IGeneRequestDispatcher, GeneRequestRegistry>();
            container.RegisterSingleton<IGeneRequestBackgroundQueue, GeneBackgroundTaskQueue>();
            container.RegisterSingleton<IGenePoolInventoryFactory, GenePoolInventoryFactory>();


            container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
            container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationTaskHandler<>), Lifestyle.Scoped);
            container.AddRebusOperationsHandlers();

            var localName = $"{QueueNames.VMHostAgent}.{Environment.MachineName}";
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