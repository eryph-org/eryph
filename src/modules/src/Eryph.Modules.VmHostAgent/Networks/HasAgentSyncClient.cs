using LanguageExt;
// ReSharper disable InconsistentNaming

namespace Eryph.Modules.VmHostAgent.Networks;

public interface HasAgentSyncClient<RT>
    where RT : struct, HasAgentSyncClient<RT>
{
    Eff<RT, ISyncClient> AgentSync { get; }
}