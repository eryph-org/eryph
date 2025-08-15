using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core;

public static class AffExtensions
{
    public static Aff<R> ToAff<R>(this EitherAsync<Error, R> either) =>
        either.ToAff(identity);

    public static EitherAsync<Error, R> ToEitherAsync<R>(this ValueTask<Fin<R>> fin) =>
        fin.AsTask().Map(f => f.ToEither()).ToAsync();

    public static async ValueTask<Fin<R>> RunWithCancel<R>(
        this Aff<CancelRt, R> aff,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        return await aff.Run(CancelRt.New(cts))
            .ConfigureAwait(false);
    }
}
