using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Rebus;
using Haipa.VmManagement;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VmHostAgentModule : ModuleBase
    {
        public override string Name => "Haipa.VmHostAgent";

        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddModuleHandler<StartBusModuleHandler>();
            services.AddModuleService<WmiWatcherModuleService>();

        }

        protected override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();
            container.RegisterSingleton<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingOperationHandler<>));

            container.ConfigureRebus(configurer => configurer
                .Transport(t => serviceProvider.GetService<IRebusTransportConfigurer>().Configure(t, "haipa.agent." + Environment.MachineName))
                .Routing(x => x.TypeBased()
                    .MapAssemblyOf<ConvergeVirtualMachineResponse>("haipa.controller"))
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                    x.EnableSynchronousRequestReply();
                })
                .Subscriptions(s => serviceProvider.GetService<IRebusSubscriptionConfigurer>().Configure(s))
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

           
        }

    }

    public class StartBusModuleHandler : IModuleHandler
    {
        private readonly IBus _bus;

        public StartBusModuleHandler(IBus bus)
        {
            _bus = bus;
        }

        public Task Execute(CancellationToken stoppingToken)
        {
            return _bus.Advanced.Topics.Subscribe("agent.all");
        }
    }
}
