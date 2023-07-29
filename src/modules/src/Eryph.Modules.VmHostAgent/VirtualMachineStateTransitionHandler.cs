using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal abstract class VirtualMachineStateTransitionHandler<T> : VCatletOperationHandlerBase<T>
        where T : class, IVMCommand, new()
    {
        public VirtualMachineStateTransitionHandler(ITaskMessaging messaging, IPowershellEngine engine) : base(messaging, engine)
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