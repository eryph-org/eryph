using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Projects;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Projects
{
    internal class UpdateProjectCommandHandler : IHandleMessages<OperationTask<UpdateProjectCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly IBus _bus;

        public UpdateProjectCommandHandler(IStateStore stateStore, IBus bus)
        {
            _stateStore = stateStore;
            _bus = bus;
        }

        public async Task Handle(OperationTask<UpdateProjectCommand> message)
        {

            var project = await _stateStore.For<Project>().GetByIdAsync(message.Command.ProjectId);
            
            if(project!= null &&  message.Command.Name!= null)
                project.Name = message.Command.Name;
            

            await _bus.SendLocal(
                OperationTaskStatusEvent.Completed(
                    message.OperationId, message.TaskId));
        }
    }
}
