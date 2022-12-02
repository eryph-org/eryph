using System;
using System.Net.Http;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.Core;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent.Images;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Rebus;
using Eryph.VmManagement;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VmHostAgentModule
    {
        public string Name => "Eryph.VmHostAgent";

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.Configure<HostOptions>(
                opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(15));


            services.AddHttpClient("eryph-hub", cfg =>
            {
                //cfg.BaseAddress = new Uri("https://eryph-images-staging.dbosoft.eu/file/eryph-images-staging/");
                cfg.BaseAddress = new Uri("https://eryph-staging-b2.b-cdn.net");
            })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHostedHandler<StartBusModuleHandler>();

            services.AddSingleton(serviceProvider.GetRequiredService<ISysEnvironment>());
            services.AddSingleton(serviceProvider.GetRequiredService<IOVNSettings>());
            services.AddOvsNode<OVSDbNode>();
            services.AddOvsNode<OVSSwitchNode>();
            services.AddOvsNode<OVNChassisNode>();
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddHostedService<WmiWatcherModuleService>();
            options.AddHostedService<ImageRequestWatcherService>();
            options.AddHostedService<SyncService>();
            options.AddHostedService<OVSChassisService>();

            options.AddLogging();
        }

        [UsedImplicitly]
        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<ISyncClient, SyncClient>();
            container.Register<IHostNetworkCommands<AgentRuntime>, HostNetworkCommands<AgentRuntime>>();
            container.Register<IOVSControl, OVSControl>();


            container.RegisterSingleton<IFileSystemService, FileSystemService>();
            container.RegisterSingleton<IAgentControlService, AgentControlService>();

            container.Register<StartBusModuleHandler>();
            container.RegisterSingleton<ITracer, Tracer>();
            container.RegisterSingleton<ITraceWriter, DiagnosticTraceWriter>();

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();

            container.Register<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>(Lifestyle.Scoped);
            container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
            container.RegisterSingleton<IHostInfoProvider, HostInfoProvider>();

            var imageSourceFactory = new ImageSourceFactory(container);
       
            imageSourceFactory.Register<LocalImageSource>(ImagesSources.Local);
            imageSourceFactory.Register<RepositoryImageSource>(ImagesSources.EryphHub);
            container.RegisterInstance<IImageSourceFactory>(imageSourceFactory);
            container.RegisterSingleton<IImageProvider, LocalFirstImageProvider>();
            container.RegisterSingleton<IImageRequestDispatcher, ImageRequestRegistry>();
            container.RegisterSingleton<IImageRequestBackgroundQueue, ImageBackgroundTaskQueue>();


            container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingTaskMessageHandler<>));
            container.RegisterDecorator(typeof(IHandleMessages<>), typeof(TraceDecorator<>));

            var localName = $"{QueueNames.VMHostAgent}.{Environment.MachineName}";
            container.ConfigureRebus(configurer => configurer
                .Transport(t =>
                    serviceProvider.GetService<IRebusTransportConfigurer>()
                        .Configure(t, localName))
                .Routing(x => x.TypeBased()
                    .Map(MessageTypes.ByRecipient(MessageRecipient.Controllers), QueueNames.Controllers)
                )
                .Options(x =>
                {
                    x.SimpleRetryStrategy(errorDetailsHeaderMaxLength:5);
                    x.SetNumberOfWorkers(5);
                    x.EnableSynchronousRequestReply();
                })
                .Subscriptions(s => serviceProvider.GetService<IRebusSubscriptionConfigurer>()?.Configure(s))
                .Logging(x => x.Trace()).Start());
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            var delay = Backoff.DecorrelatedJitterBackoffV2(
                TimeSpan.FromSeconds(1), 5);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<HttpRequestException>(ex =>
                {
                    return true;
                })
                .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                    retryAttempt)));
        }
    }
}