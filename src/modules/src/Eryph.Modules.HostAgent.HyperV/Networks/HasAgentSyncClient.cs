using LanguageExt;
// ReSharper disable InconsistentNaming

namespace Eryph.Modules.HostAgent.Networks;

public interface HasAgentSyncClient<RT>
    where RT : struct, HasAgentSyncClient<RT>
{
    Eff<RT, ISyncClient> AgentSync { get; }
}