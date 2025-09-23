using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Networks;

internal sealed class ProviderNetworkUpdateState<RT> : IDisposable
    where RT : struct, HasCancel<RT>
{
    private readonly List<NetworkChangeOperation<RT>> _executedOperations = new();

    public Eff<Unit> AddExecutedOperation(NetworkChangeOperation<RT> operation)
    {
        _executedOperations.Add(operation);
        return unitEff;
    }

    public Eff<Seq<NetworkChangeOperation<RT>>> GetExecutedOperations() =>
        SuccessEff(_executedOperations.ToSeq().Strict());

    public void Dispose()
    {
        // The IDisposable interface is only implemented to provide
        // support for the use() function.
    }
}
