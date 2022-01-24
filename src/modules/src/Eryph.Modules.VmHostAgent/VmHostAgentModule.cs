using System;
using Dbosoft.Hosuto.HostedServices;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.Rebus;
using Eryph.VmManagement;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Logging;
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
            services.AddHostedHandler<StartBusModuleHandler>();
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddHostedService<WmiWatcherModuleService>();
        }

        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<StartBusModuleHandler>();
            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();
            container.RegisterSingleton<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingTaskMessageHandler<>));

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
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings
                    {TypeNameHandling = TypeNameHandling.None}))
                .Logging(x => x.Trace()).Start());
        }
    }
}