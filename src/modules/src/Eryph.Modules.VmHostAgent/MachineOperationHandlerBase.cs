using System;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.Modules.VmHostAgent
{
    internal abstract class MachineOperationHandlerBase<T> : IHandleMessages<OperationTask<T>>
        where T : class, IVMCommand, new()
    {
        private readonly IBus _bus;
        private readonly IPowershellEngine _engine;

        protected MachineOperationHandlerBase(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
        }

        public async Task Handle(OperationTask<T> message)
        {
            var command = message.Command;

            var result = await GetVmInfo(command.VMId, _engine)
                .BindAsync(optVmInfo =>
                {
                    return optVmInfo.MatchAsync(
                        Some: s => HandleCommand(s, command, _engine),
                        None: () => Unit.Default);
                }).ConfigureAwait(false);

            await result.MatchAsync(
                LeftAsync: f => HandleError(f, message),
                RightAsync: async _ =>
                {
                    await _bus.Publish(OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId))
                        .ConfigureAwait(false);

                    return Unit.Default;
                }).ConfigureAwait(false);
        }

        protected abstract Task<Either<PowershellFailure, Unit>> HandleCommand(
            TypedPsObject<VirtualMachineInfo> vmInfo, T command, IPowershellEngine engine);

        private async Task<Unit> HandleError(PowershellFailure failure, IOperationTaskMessage message)
        {
            await _bus.Publish(OperationTaskStatusEvent.Failed(message.OperationId, message.TaskId,
                new ErrorData
                {
                    ErrorMessage = failure.Message
                })).ConfigureAwait(false);

            return Unit.Default;
        }

        private Task<Either<PowershellFailure, Option<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(Guid vmId,
            IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(CreateGetVMCommand(vmId))
                .MapAsync(seq => seq.HeadOrNone());
        }

        protected virtual PsCommandBuilder CreateGetVMCommand(Guid vmId)
        {
            return PsCommandBuilder.Create()
                .AddCommand("get-vm").AddParameter("Id", vmId);
        }
    }
}