using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal abstract class VirtualMachineStateTransitionHandler<T>(
    ITaskMessaging messaging,
    IPowershellEngine engine)
    : CatletOperationHandlerBase<T>(messaging, engine)
    where T : class, IVMCommand, new()
{
    private readonly IPowershellEngine _engine = engine;

    protected override EitherAsync<Error, Unit> HandleCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        T command) =>
        _engine.RunAsync(CreateTransitionCommand(vmInfo))
            .ToError()
            .ToAsync();

    protected abstract PsCommandBuilder CreateTransitionCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo);
}
