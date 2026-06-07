using System;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.ModuleCore.Networks;
using Eryph.Rebus;
using SimpleInjector;

namespace Eryph.Network
{
    /// <summary>
    /// Root-container registrations that <see cref="Eryph.Modules.Network.NetworkModule"/> resolves
    /// through the cross-wired host service provider. The OVN settings/environment and the enrolled
    /// certificate store are registered on the module container by the host filter in
    /// <see cref="HostNetworkModuleExtensions"/>.
    /// </summary>
    internal static class NetworkContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            // Bridges the OVN control plane to a host-agent chassis. The standalone network process
            // has no in-process chassis, so this stays recipient-less: a control event finds no
            // recipient and returns false (each agent stops its own ovn-controller).
            container.RegisterSingleton<IAgentControlService, AgentControlService>();

            // The module publishes operation results/events to the controller over the bus.
            container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish,
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
                DeferCompletion = TimeSpan.FromMinutes(1),
                JsonSerializerOptions = EryphJsonSerializerOptions.Options,
            });
        }
    }
}
