using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Messages.Projects;
using Eryph.Messages.Resources.Commands;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Projects
{
    [UsedImplicitly]
    internal class DestroyProjectSaga : OperationTaskWorkflowSaga<DestroyProjectCommand, DestroyProjectSagaData>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IStateStore _stateStore;

        public DestroyProjectSaga(IWorkflow workflow, IStateStore stateStore) : base(workflow)
        {
            _stateStore = stateStore;
        }


        protected override void CorrelateMessages(ICorrelationConfig<DestroyProjectSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
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
            {
                var roleAssignments = await _stateStore.For<ProjectRoleAssignment>()
                    .ListAsync(new ProjectRoleAssignmentSpecs.GetByProject(project.Id))
                    .ConfigureAwait(false);

                await _stateStore.For<ProjectRoleAssignment>().DeleteRangeAsync(roleAssignments).ConfigureAwait(false);
                await _stateStore.For<Project>().DeleteAsync(project).ConfigureAwait(false);
            }
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