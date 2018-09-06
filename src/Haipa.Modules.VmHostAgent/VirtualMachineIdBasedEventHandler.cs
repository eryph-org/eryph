using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    internal abstract class MachineOperationHandlerBase<T> : IHandleMessages<AcceptedOperation<T>> where T: OperationCommand, IMachineCommand
    {
        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;

        protected MachineOperationHandlerBase(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
        }

        protected abstract Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> HandleCommand(
            TypedPsObject<VirtualMachineInfo> vmInfo, T command, IPowershellEngine engine);

        public async Task Handle(AcceptedOperation<T> message)
        {
            var command = message.Command;

            var result = await GetVmInfo(command.MachineId, _engine)
                .BindAsync(vmInfo => HandleCommand(vmInfo, command, _engine)).ConfigureAwait(false);
            
            await result.MatchAsync(
                LeftAsync: f => HandleError(f,command),
                RightAsync: async vmInfo =>
                {
                    await _bus.Send(new OperationCompletedEvent
                    {
                        OperationId = command.OperationId,

                    }).ConfigureAwait(false);

                    return Unit.Default;
                }).ConfigureAwait(false);
        }

        private async Task<Unit> HandleError(PowershellFailure failure, OperationCommand command)
        {
            await _bus.Send(new OperationFailedEvent(){
                OperationId = command.OperationId,
                ErrorMessage = failure.Message

            }).ConfigureAwait(false);

            return Unit.Default;
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetVmInfo(Guid vmId,
            IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                .AddCommand("get-vm").AddParameter("Id", vmId)).MapAsync(seq => seq.Head);
        }
    }
}