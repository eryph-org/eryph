using System.Threading;
using LanguageExt.Effects.Traits;

namespace Eryph.Core.Sys;

/// <summary>
/// Minimal runtime for LanguageExt which only provides <see cref="HasCancel{RT}"/>.
/// </summary>
/// <remarks>
/// Some scheduling and cancellation features of <see cref="LanguageExt.Aff{A}"/>
/// require a runtime with <see cref="HasCancel{RT}"/> support. This runtime allows
/// to use these features without a purpose-build runtime.
/// </remarks>
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
