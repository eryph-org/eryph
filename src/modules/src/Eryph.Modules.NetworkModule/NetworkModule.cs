using System;
using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network
{
    [UsedImplicitly]
    public class NetworkModule
    {

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider sp, IServiceCollection services)
        {
            services.AddSingleton(sp.GetRequiredService<ISysEnvironment>());
            services.AddSingleton(sp.GetRequiredService<IOVNSettings>());
            services.AddSingleton(sp.GetRequiredService<IAgentControlService>());

            services.AddOvsNode<SyncedOVNDatabaseNode>();
            services.AddOvsNode<NetworkControllerNode>();

        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {


            options.AddHostedService<OVSNodeHostedService<SyncedOVNDatabaseNode>>();
            options.AddHostedService<OVSNodeHostedService<NetworkControllerNode>>();

            options.AddLogging();
        }

    }


    public class SyncedOVNDatabaseNode : OVNDatabaseNode
    {
        private readonly IAgentControlService _agentControlService;

        public SyncedOVNDatabaseNode(
            IAgentControlService agentControlService,
            ISysEnvironment sysEnv, IOVNSettings ovnSettings, ILoggerFactory loggerFactory) : base(sysEnv, ovnSettings, loggerFactory)
        {
            _agentControlService = agentControlService;
        }

        public override EitherAsync<Error, Unit> Stop(bool ensureNodeStopped, CancellationToken cancellationToken = new CancellationToken())
        {
            return 
                from stopController in Prelude.TryAsync(() =>_agentControlService.SendControlEvent(AgentService.OVNController, AgentServiceOperation.Stop,
                    cancellationToken)).ToEither()
                from stopDb in base.Stop(ensureNodeStopped, cancellationToken)
                select Unit.Default;
        }
    }
}