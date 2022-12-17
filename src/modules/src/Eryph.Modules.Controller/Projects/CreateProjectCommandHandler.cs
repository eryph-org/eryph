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
    internal class CreateProjectCommandHandler : IHandleMessages<OperationTask<CreateProjectCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly IBus _bus;

        public CreateProjectCommandHandler(IStateStore stateStore, IBus bus)
        {
            _stateStore = stateStore;
            _bus = bus;
        }

        public async Task Handle(OperationTask<CreateProjectCommand> message)
        {
            await _stateStore.For<Project>().AddAsync(
                new Project { Id = message.Command.CorrelationId, Name = message.Command.Name, 
                    TenantId = EryphConstants.DefaultTenantId }
                );
            
            await _bus.SendLocal(
                OperationTaskStatusEvent.Completed(
                    message.OperationId, message.TaskId, new ProjectReference{ ProjectId = message.Command.CorrelationId}));
        }
    }
}
