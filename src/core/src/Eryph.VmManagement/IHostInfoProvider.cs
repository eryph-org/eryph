using Eryph.Resources.Machines;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface IHostInfoProvider
{
    EitherAsync<Error, VMHostMachineData> GetHostInfoAsync(bool refresh=false);

}