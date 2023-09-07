using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Network;

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