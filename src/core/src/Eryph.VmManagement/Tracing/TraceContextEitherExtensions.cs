using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement;

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
}