using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Events;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using Haipa.VmManagement;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    internal class UpdateVirtualMachineMetadataCommandHandler : VirtualMachineConfigCommandHandler, IHandleMessages<AcceptedOperationTaskEvent<UpdateVirtualMachineMetadataCommand>>
    {

        public UpdateVirtualMachineMetadataCommandHandler(IPowershellEngine engine, IBus bus) : base(engine, bus)
        {
        }


        public Task Handle(AcceptedOperationTaskEvent<UpdateVirtualMachineMetadataCommand> message)
        {
            var command = message.Command;
            OperationId = command.OperationId;
            TaskId = command.TaskId;

            var metadata = new VirtualMachineMetadata{Id = command.CurrentMetadataId};

            var chain =

                from vmList in GetVmInfo(command.VMId, Engine).ToAsync()
                from vmInfo in EnsureSingleEntry(vmList, command.VMId).ToAsync()

                from currentMetadata in EnsureMetadata(metadata, vmInfo).ToAsync()
                from _ in SetMetadataId(vmInfo, command.NewMetadataId).ToAsync()
                select Unit.Default;

            return chain.MatchAsync(
                LeftAsync: HandleError,
                RightAsync: result => Bus.Publish(OperationTaskStatusEvent.Completed(OperationId, TaskId)));

        }
    }
}