using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Projects;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.Operations;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Pipeline;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Projects
{
    [UsedImplicitly]
    internal class DestroyProjectSaga : OperationTaskWorkflowSaga<DestroyProjectCommand, DestroyProjectSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IStateStore _stateStore;

        public DestroyProjectSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IMessageContext messageContext, IStateStore stateStore) : base(bus, taskDispatcher, messageContext)
        {
            _stateStore = stateStore;
        }


        protected override void CorrelateMessages(ICorrelationConfig<DestroyProjectSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(m => m.OperationId, d => d.OperationId);
        }


        protected override async Task Initiated(DestroyProjectCommand message)
        {
            Data.ProjectId = message.ProjectId;

            if (Data.ProjectId == EryphConstants.DefaultProjectId)
            {
                await Fail(new ErrorData { ErrorMessage = "Default project cannot be deleted" });
                return;
            }

            var project = await _stateStore.For<Project>().GetByIdAsync(Data.ProjectId);

            if (project == null)
            {
                await Complete();
                return;
            }

            await _stateStore.LoadCollectionAsync(project, x => x.Resources);

            if (project.Resources.Count == 0)
            {
                await DeleteProject();
                await Complete();
                return;
            }

            await StartNewTask(new DestroyResourcesCommand
            {
                Resources = project.Resources.Select(x=> new Resource(x.ResourceType, x.Id)).ToArray()
            });


        }

        private async Task DeleteProject()
        {
            var project = await _stateStore.For<Project>().GetByIdAsync(Data.ProjectId);

            if (project != null)
                await _stateStore.For<Project>().DeleteAsync(project);

        }

        public Task Handle(OperationTaskStatusEvent<DestroyResourcesCommand> message)
        {
            return FailOrRun<DestroyResourcesCommand, DestroyResourcesResponse>(message,
                async (response) =>
                {
                    await DeleteProject();
                    await Complete(response);
                });
        }

    }
}