using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public abstract class ConvergeTaskBase
    {
        protected readonly ConvergeContext Context;

        protected ConvergeTaskBase(ConvergeContext context)
        {
            Context = context;
        }

        public abstract Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo);
    }
}