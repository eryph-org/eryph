using System;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;

using PsVMResult = LanguageExt.Either<Haipa.VmManagement.PowershellFailure, LanguageExt.Unit>;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class DestroyVirtualMachineHandler : MachineOperationHandlerBase<DestroyMachineCommand>
    {

        public DestroyVirtualMachineHandler(IBus bus, IPowershellEngine engine) : base(bus, engine)
        {
        }
        
        protected override Task<PsVMResult> HandleCommand(TypedPsObject<VirtualMachineInfo> vmInfo, DestroyMachineCommand command, IPowershellEngine engine)
        {
            return vmInfo
                .StopIfRunning(engine)
                .BindAsync(v => v.Remove(engine).MapAsync(_ => Unit.Default));
        }

        protected override PsCommandBuilder CreateGetVMCommand(Guid vmId)
        {
            return base.CreateGetVMCommand(vmId).AddParameter("ErrorAction", "SilentlyContinue");
        }
    }
}