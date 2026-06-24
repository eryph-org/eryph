using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging;

public abstract class ConvergeTaskBase(ConvergeContext context)
{
    protected readonly ConvergeContext Context = context;

    public abstract Task<Either<Error, Unit>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo);
}
