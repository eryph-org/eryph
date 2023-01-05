using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Tracing;

public static class TraceContextEitherExtensions
{
    public static Task<Either<PowershellFailure, T>> WriteTrace<T>(this Task<Either<PowershellFailure, T>> either, [CallerArgumentExpression("either")] string name = null)
    {
        return either.MapT(r =>
        {
            TraceContext.Current.Write(StateTraceData.FromObject(r, name));
            return r;
        });

    }

    public static EitherAsync<PowershellFailure, T> WriteTrace<T>(this EitherAsync<PowershellFailure, T> either, [CallerArgumentExpression("either")] string name = null)
    {
        return either.Map(r =>
        {
            TraceContext.Current.Write(StateTraceData.FromObject(r, name));
            return r;
        });

    }

    public static Task<Either<Error, T>> WriteTrace<T>(this Task<Either<Error, T>> either, [CallerArgumentExpression("either")] string name = null)
    {
        return either.MapT(r =>
        {
            TraceContext.Current.Write(StateTraceData.FromObject(r, name));
            return r;
        });

    }

    public static EitherAsync<Error, T> WriteTrace<T>(this EitherAsync<Error, T> either, [CallerArgumentExpression("either")] string name = null)
    {
        return either.Map(r =>
        {
            TraceContext.Current.Write(StateTraceData.FromObject(r, name));
            return r;
        });

    }
}