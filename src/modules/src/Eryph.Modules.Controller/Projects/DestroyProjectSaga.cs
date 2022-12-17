using System.Linq;
using System.Threading.Tasks;
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
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Projects
{
    [UsedImplicitly]
    internal class DestroyProjectSaga : OperationTaskWorkflowSaga<DestroyProjectCommand, DestroyProjectSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IStateStore _stateStore;

        public DestroyProjectSaga(IBus bus, IOperationTaskDispatcher taskDispatcher, IStateStore stateStore) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
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

            await _taskDispatcher.StartNew(Data.OperationId, new DestroyResourcesCommand
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