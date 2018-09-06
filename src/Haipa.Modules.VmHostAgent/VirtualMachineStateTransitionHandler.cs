using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal abstract class VirtualMachineStateTransitionHandler<T> : MachineOperationHandlerBase<T> where T : OperationCommand, IMachineCommand
    {

        public VirtualMachineStateTransitionHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }

        protected abstract string TransitionPowerShellCommand { get; }

        protected override Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> HandleCommand(TypedPsObject<VirtualMachineInfo> vmInfo, T command, IPowershellEngine engine) =>
            engine.RunAsync(new PsCommandBuilder().AddCommand(TransitionPowerShellCommand).AddParameter("VM", vmInfo.PsObject))
                .MapAsync(u => vmInfo.Recreate());
    }
}