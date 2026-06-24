using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class PowershellEngineExtensions
{
    extension(IPowershellEngine engine)
    {
        public EitherAsync<Error, Option<TypedPsObject<T>>> GetObjectAsync<T>(PsCommandBuilder builder,
            Func<int, Task>? reportProgress = null,
            bool withoutLock = false,
            CancellationToken cancellationToken = default) =>
            from results in engine.GetObjectsAsync<T>(builder, reportProgress, withoutLock, cancellationToken)
            from _ in guard(results.Count <= 1,
                Error.New($"Powershell returned multiple values when fetching {typeof(T).Name}."))
            select results.HeadOrNone();

        public EitherAsync<Error, Option<T>> GetObjectValueAsync<T>(PsCommandBuilder builder,
            Func<int, Task>? reportProgress = null,
            bool withoutLock = false,
            CancellationToken cancellationToken = default) =>
            from results in engine.GetObjectValuesAsync<T>(builder, reportProgress, withoutLock, cancellationToken)
            from _ in guard(results.Count <= 1,
                Error.New($"Powershell returned multiple values when fetching {typeof(T).Name}."))
            select results.HeadOrNone();
    }
}
