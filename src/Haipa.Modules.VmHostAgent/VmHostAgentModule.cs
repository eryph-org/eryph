using System;
using Haipa.Messages;
using Haipa.Modules.Abstractions;
using Haipa.Rebus;
using Haipa.VmManagement;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class VmHostAgentModule : IModule
    {
        public string Name => "Haipa.VmHostAgent";

        private readonly Container _globalContainer;
        private WmiWatcher _wmiWatcher;

        public VmHostAgentModule(Container globalContainer)
        {
            _globalContainer = globalContainer;
        }
        
        public void Start()
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();
            container.RegisterSingleton<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(VmHostAgentModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingOperationHandler<>));

            container.ConfigureRebus(configurer => configurer
                .Transport(t => _globalContainer.GetInstance<IRebusTransportConfigurer>().Configure(t, "haipa.agent." + Environment.MachineName ))
                .Routing(x => x.TypeBased()
                    .MapAssemblyOf<ConvergeVirtualMachineResponse>("haipa.controller"))
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Subscriptions(s => _globalContainer.GetInstance<IRebusSubscriptionConfigurer>().Configure(s) )
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

            container.StartBus();
            var bus = container.GetInstance<IBus>();
            bus.Advanced.Topics.Subscribe("agent.all");

            _wmiWatcher = new WmiWatcher(bus);
            _wmiWatcher.StartWatching();
        }

        public void Stop()
        {
            _wmiWatcher.Dispose();
            _wmiWatcher = null;
        }
    }
}
