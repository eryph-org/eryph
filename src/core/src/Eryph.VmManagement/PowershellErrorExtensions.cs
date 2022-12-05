using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public static class PowershellErrorExtensions
{
    public static Either<Error, TR> ToError<TR>(this Either<PowershellFailure, TR> either)
        => either.MapLeft(l => Error.New(l.Message));


    public static Task<Either<Error,TR>> ToError<TR>(this Task<Either<PowershellFailure, TR>> either)
        => either.Map( e => e.ToError());

    public static EitherAsync<Error,TR> ToError<TR>(this EitherAsync<PowershellFailure, TR> either)
        => either.MapLeft(l => Error.New(l.Message));

}