using System.Threading.Tasks;
using Haipa.VmManagement.Data.Full;
using LanguageExt;

namespace Haipa.VmManagement.Converging
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