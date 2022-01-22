using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement
{
    public static class MonadHelpers
    {
        public static Task<Either<TL, Option<TR>>> LastOrNone<TL, TR>(this Task<Either<TL, Seq<TR>>> either)
        {
            return either.MapAsync(s => s.LastOrNone());
        }

        public static Task<Either<TL, Seq<TR>>> MapToEitherAsync<TL, TR, TEntry>(this Seq<TEntry> sequence,
            Func<int, TEntry, Task<Either<TL, TR>>> mapperFunc)
        {
            return sequence.Map(mapperFunc).ToImmutableArray()
                .Traverse(l => l)
                .Bind(e =>
                {
                    var enumerable = e as Either<TL, TR>[] ?? e.ToArray();
                    return enumerable.Lefts().HeadOrNone()
                        .MatchAsync(
                            s => LeftAsync<TL, Seq<TR>>(s).ToEither(),
                            () => RightAsync<TL, Seq<TR>>(enumerable.Rights().ToSeq()).ToEither());
                });
        }

        public static Task<Either<TL, Seq<TR>>> MapToEitherAsync<TL, TR, TEntry>(this Seq<TEntry> sequence,
            Func<TEntry, Task<Either<TL, TR>>> mapperFunc)
        {
            return sequence.Map(mapperFunc).ToImmutableArray()
                .Traverse(l => l)
                .Bind(e =>
                {
                    var enumerable = e as Either<TL, TR>[] ?? e.ToArray();
                    return enumerable.Lefts().HeadOrNone()
                        .MatchAsync(
                            s => LeftAsync<TL, Seq<TR>>(s).ToEither(),
                            () => RightAsync<TL, Seq<TR>>(enumerable.Rights().ToSeq()).ToEither());
                });
        }
    }
}