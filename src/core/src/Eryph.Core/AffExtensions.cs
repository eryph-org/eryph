using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core;

public static class AffExtensions
{
    public static Aff<R> ToAff<R>(this EitherAsync<Error, R> either) =>
        either.ToAff(identity);
}
