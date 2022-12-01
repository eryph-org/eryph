using System.Threading.Tasks;
using Eryph.Messages.Operations.Commands;
using Eryph.Messages.Resources.Machines;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal abstract class VirtualMachineStateTransitionHandler<T> : MachineOperationHandlerBase<T>
        where T : class, IVMCommand, new()
    {
        public VirtualMachineStateTransitionHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected abstract string TransitionPowerShellCommand { get; }

        protected override async Task<Either<Error, Unit>> HandleCommand(
            TypedPsObject<VirtualMachineInfo> vmInfo,
            T command, IPowershellEngine engine)
        {
            var result = await engine.RunAsync(new PsCommandBuilder().AddCommand(TransitionPowerShellCommand)
                .AddParameter("VM", vmInfo.PsObject)).ConfigureAwait(false);

            return result.ToError();
        }
    }
}