using Haipa.Modules.Abstractions;
using HyperVPlus.Messages;
using HyperVPlus.Rebus;
using HyperVPlus.VmManagement;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    public class VmHostAgentModule : IModule
    {
        public string Name => "Haipa.VmHostAgent";

        private readonly Container _globalContainer;

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

            container.ConfigureRebus(configurer => configurer
                .Transport(t => _globalContainer.GetInstance<IRebusTransportConfigurer>().Configure(t, "haipa.agent.localhost"))
                .Routing(x => x.TypeBased()
                    .Map<ConvergeVirtualMachineResponse>("haipa.controller")
                    .Map<ConvergeVirtualMachineProgressEvent>("haipa.controller"))
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Subscriptions(s => _globalContainer.GetInstance<IRebusSubscriptionConfigurer>().Configure(s))

                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

            container.StartBus();

        }

        public void Stop()
        {
        }
    }


}
