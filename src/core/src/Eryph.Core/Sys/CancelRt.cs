using System.Threading;
using LanguageExt.Effects.Traits;

namespace Eryph.Core.Sys;

public readonly struct CancelRt : HasCancel<CancelRt>
{
    private readonly CancellationTokenSource _cts;

    private CancelRt(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    public static CancelRt New(CancellationTokenSource cancellationTokenSource) => new(cancellationTokenSource);

    public CancelRt LocalCancel => new(new CancellationTokenSource());

    public CancellationToken CancellationToken => _cts.Token;

    public CancellationTokenSource CancellationTokenSource => _cts;
}
