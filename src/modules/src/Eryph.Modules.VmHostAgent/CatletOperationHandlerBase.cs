using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

internal abstract class CatletOperationHandlerBase<T>(
    ITaskMessaging messaging,
    IPowershellEngine engine)
    : IHandleMessages<OperationTask<T>>
    where T : class, IVMCommand, new()
{
    public Task Handle(OperationTask<T> message)
    {
        var command = message.Command;

        return GetVmInfo(command.VMId)
            .Bind(optVmInfo =>
            {
                return optVmInfo.Match(
                    Some: s => HandleCommand(s, command),
                    None: () => Unit.Default);
            })
            .FailOrComplete(messaging, message);
    }

    protected abstract EitherAsync<Error, Unit> HandleCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        T command);


    private EitherAsync<Error, Option<TypedPsObject<VirtualMachineInfo>>> GetVmInfo(
        Guid vmId) =>
        engine.GetObjectsAsync<VirtualMachineInfo>(CreateGetVMCommand(vmId))
            .Map(seq => seq.HeadOrNone());

    protected virtual PsCommandBuilder CreateGetVMCommand(Guid vmId) =>
        PsCommandBuilder.Create().AddCommand("Get-VM").AddParameter("Id", vmId);
}
