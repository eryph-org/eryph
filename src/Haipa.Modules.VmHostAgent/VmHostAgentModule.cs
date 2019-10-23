using System;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Haipa.Rebus;
using Haipa.VmManagement;
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
                    .Map(MessageTypes.ByOwner(MessageOwner.Controllers), "haipa.controllers")
                    //.Map(MessageTypes.ByOwner(MessageOwner.TaskQueue), "haipa.taskqueue")
                )                
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                    x.EnableSynchronousRequestReply();
                })
                .Subscriptions(s => serviceProvider.GetService<IRebusSubscriptionConfigurer>()?.Configure(s))
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

           
        }

    }
}
