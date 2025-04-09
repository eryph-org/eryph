using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class PowershellEngineExtensions
{
    public static EitherAsync<Error, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        this IPowershellEngine engine,
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        bool withoutLock = false,
        CancellationToken cancellationToken = default) =>
        from results in engine.GetObjectsAsync<T>(builder, reportProgress, withoutLock, cancellationToken)
        from _ in guard(results.Count <= 1,
            Error.New($"Powershell returned multiple values when fetching {typeof(T).Name}."))
        select results.HeadOrNone();

    public static EitherAsync<Error, Option<T>> GetObjectValueAsync<T>(
        this IPowershellEngine engine,
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        bool withoutLock = false,
        CancellationToken cancellationToken = default) =>
        from results in engine.GetObjectValuesAsync<T>(builder, reportProgress, withoutLock, cancellationToken)
        from _ in guard(results.Count <= 1,
            Error.New($"Powershell returned multiple values when fetching {typeof(T).Name}."))
        select results.HeadOrNone();
}
