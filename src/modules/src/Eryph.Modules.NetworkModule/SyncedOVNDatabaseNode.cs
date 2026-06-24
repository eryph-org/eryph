using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Eryph.ModuleCore.Networks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Network;

public class SyncedOVNDatabaseNode(
    IAgentControlService agentControlService,
    ISystemEnvironment systemEnvironment,
    IOVNSettings ovnSettings,
    ILoggerFactory loggerFactory)
    : OVNDatabaseNode(systemEnvironment, ovnSettings, loggerFactory)
{
    public override EitherAsync<Error, Unit> Stop(
        bool ensureNodeStopped,
        CancellationToken cancellationToken = default) =>
        from stopController in Prelude.TryAsync(() => agentControlService.SendControlEvent(
                AgentService.OVNController,
                AgentServiceOperation.Stop,
                cancellationToken))
            .ToEither()
        from stopDb in base.Stop(ensureNodeStopped, cancellationToken)
        select Unit.Default;
}
