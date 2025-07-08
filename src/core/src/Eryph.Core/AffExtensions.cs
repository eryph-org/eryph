using System.Threading.Tasks;
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
}
