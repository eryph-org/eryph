using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    internal class UpdateVirtualMachineMetadataCommandHandler : VirtualMachineConfigCommandHandler,
        IHandleMessages<OperationTask<UpdateVirtualMachineMetadataCommand>>
    {
        public UpdateVirtualMachineMetadataCommandHandler(IPowershellEngine engine, IBus bus, ILogger log) : base(engine, bus, log)
        {
        }


        public Task Handle(OperationTask<UpdateVirtualMachineMetadataCommand> message)
        {
            var command = message.Command;
            OperationId = message.OperationId;
            TaskId = message.TaskId;

            var metadata = new VirtualMachineMetadata {Id = command.CurrentMetadataId};

            var chain =
                from vmList in GetVmInfo(command.VMId, Engine)
                from vmInfo in EnsureSingleEntry(vmList, command.VMId)
                from currentMetadata in EnsureMetadata(metadata, vmInfo).ToAsync()
                from _ in SetMetadataId(vmInfo, command.NewMetadataId)
                select Unit.Default;

            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: result => Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId)));
        }
    }
}