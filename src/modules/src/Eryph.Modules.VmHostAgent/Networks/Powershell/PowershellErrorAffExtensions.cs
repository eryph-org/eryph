using System.Threading.Tasks;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.Powershell;

public static class PowershellErrorAffExtensions
{
    public static Aff<TR> ToAff<TR>(this Task<Either<PowershellFailure, TR>> either)
        => either.ToAsync().ToAff(l => Error.New(l.Message));

    public static Aff<TR> ToAff<TR>(this EitherAsync<PowershellFailure, TR> either)
        => either.ToAff(l => Error.New(l.Message));

}