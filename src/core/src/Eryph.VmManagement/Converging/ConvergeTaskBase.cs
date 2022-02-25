using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.VmManagement.Converging
{
    public abstract class ConvergeTaskBase
    {
        protected readonly ConvergeContext Context;

        protected ConvergeTaskBase(ConvergeContext context)
        {
            Context = context;
        }

        public abstract Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo);
    }
}