using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Haipa.Modules.VmHostAgent
{
    public static class MonadHelpers
    {
        public static Task<Either<TL, Option<TR>>> LastOrNone<TL, TR>(this Task<Either<TL, Seq<TR>>> either)
        {
            return either.MapAsync(s => s.LastOrNone());

        }

        public static Task<Either<TL, Seq<TR>>> MapToEitherAsync<TL, TR, TEntry>(this ISeq<TEntry> sequence, Func<TEntry, Task<Either<TL, TR>>> mapperFunc)
        {
            return sequence.Map(mapperFunc)
                .Traverse(l => l)
                .Bind(e => e.Lefts().HeadOrNone()
                    .MatchAsync(
                        Some: s => LeftAsync<TL, Seq<TR>>(s).ToEither(),
                        None: () => RightAsync<TL, Seq<TR>>(e.Rights()).ToEither()));

        }
    }
}
