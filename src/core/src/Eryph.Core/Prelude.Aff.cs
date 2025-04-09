using System;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Core;

public static partial class Prelude
{
    /// <summary>
    /// Sleeps for the given time interval.
    /// </summary>
    /// <remarks>
    /// This method is easier to use than the one provided by LanguageExt
    /// which require a runtime trait.
    /// </remarks>
    public static Aff<RT, Unit> sleep<RT>(
        TimeSpan timeSpan)
        where RT : struct, HasCancel<RT> =>
        from ct in cancelToken<RT>()
        from _ in Aff(async () => await Task.Delay(timeSpan, ct).ToUnit())
        select unit;
}
