using System;
using Dbosoft.Rebus.Operations;
using Eryph.AppCore;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.HostAgent;
using Eryph.Rebus;
using SimpleInjector;

namespace Eryph.Agent
{
    internal static class AgentContainerExtensions
    {
        /// <summary>
        /// Root-container registrations that <see cref="Eryph.Modules.HostAgent.VmHostAgentModule"/>
        /// resolves through the cross-wired host service provider. The bus transport and OVN
        /// environment are registered on the module container via the host filters in
        /// <see cref="HostVmHostAgentModuleExtensions"/>.
        /// </summary>
        public static void Bootstrap(this Container container)
        {
            container.Register<INetworkProviderManager, NetworkProviderManager>();
            container.RegisterSingleton<IAgentControlService, AgentControlService>();
            container.RegisterSingleton<IHostSettingsProvider, HostSettingsProvider>();
            container.RegisterSingleton<IVmHostAgentConfigurationManager, AgentVmHostAgentConfigurationManager>();
            container.RegisterSingleton<IApplicationInfoProvider, AgentApplicationInfoProvider>();

            // Network synchronization is owned by the controller; the agent only needs a
            // placeholder so its named-pipe admin interface can resolve (see the type docs).
            container.RegisterSingleton<INetworkSyncService, UnavailableNetworkSyncService>();

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
