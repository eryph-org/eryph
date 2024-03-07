using System.Threading.Tasks;
using System.Xml.Linq;
using Dbosoft.Rebus.Operations;
using Eryph.Messages;
using Eryph.ConfigModel;
using Eryph.Messages.Projects;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Projects
{
    [UsedImplicitly]
    internal class UpdateProjectCommandHandler : IHandleMessages<OperationTask<UpdateProjectCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly ITaskMessaging _messaging;

        public UpdateProjectCommandHandler(IStateStore stateStore, ITaskMessaging messaging)
        {
            _stateStore = stateStore;
            _messaging = messaging;
        }

        public async Task Handle(OperationTask<UpdateProjectCommand> message)
        {
            var name = message.Command.Name;

            var validation = ProjectName.NewValidation(name);
            if (validation.IsFail)
            {
                await _messaging.FailTask(message, validation);
                return;
            }

            if (name == "default")
            {
                await _messaging.FailTask(message, $"The project name '{name}' is reserved.");
                return;
            }

            var project = await _stateStore.For<Project>().GetByIdAsync(message.Command.ProjectId);
            if (project is not null)
                project.Name = name;
            

            await _messaging.CompleteTask(message, new ProjectReference
            {
                ProjectId = message.Command.CorrelationId,
                ProjectName = project?.Name
            });
        }
    }
}
