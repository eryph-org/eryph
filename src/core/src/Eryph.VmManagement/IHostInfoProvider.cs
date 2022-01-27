using System.Threading.Tasks;
using Eryph.Resources.Machines;
using LanguageExt;

namespace Eryph.VmManagement;

public interface IHostInfoProvider
{
    Task<Either<PowershellFailure, VMHostMachineData>> GetHostInfoAsync(bool refresh=false);

}