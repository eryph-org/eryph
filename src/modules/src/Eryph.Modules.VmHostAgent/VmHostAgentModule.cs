using System;
using System.Net.Http;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Core;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent.Images;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.Rebus;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VmHostAgentModule
    {
        public string Name => "Eryph.VmHostAgent";

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddHttpClient("eryph-hub", cfg =>
            {
                //cfg.BaseAddress = new Uri("https://eryph-images-staging.dbosoft.eu/file/eryph-images-staging/");
                cfg.BaseAddress = new Uri("https://eryph-staging-b2.b-cdn.net");
            })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))  //Set lifetime to five minutes
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHostedHandler<StartBusModuleHandler>();
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddHostedService<WmiWatcherModuleService>();
            options.AddHostedService<ImageRequestWatcherService>();
            options.AddLogging();
        }

        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.RegisterSingleton<IFileSystemService, FileSystemService>();


            container.Register<StartBusModuleHandler>();
            container.RegisterSingleton<ITracer, Tracer>();
            container.RegisterSingleton<ITraceWriter, DiagnosticTraceWriter>();

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();
            container.RegisterSingleton<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>();
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


            container.ConfigureRebus(configurer => configurer
                .Transport(t =>
                    serviceProvider.GetService<IRebusTransportConfigurer>()
                        .Configure(t, $"{QueueNames.VMHostAgent}.{Environment.MachineName}"))
                .Routing(x => x.TypeBased()
                    .Map(MessageTypes.ByRecipient(MessageRecipient.Controllers), QueueNames.Controllers)
                )
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
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