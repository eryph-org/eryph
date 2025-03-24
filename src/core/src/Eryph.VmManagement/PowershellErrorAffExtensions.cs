using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public static class PowershellErrorAffExtensions
{
    public static Aff<TR> ToAff<TR>(this EitherAsync<Error, TR> either)
        => either.ToAff(l => l);
}
