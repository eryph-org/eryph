using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Compute;


[UsedImplicitly]
internal class CreateCatletSpecificationSaga(
    IStateStore stateStore,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CreateCatletSpecificationCommand, EryphSagaData<CreateCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{

    protected override async Task Initiated(CreateCatletSpecificationCommand message)
    {
        Data.Data.State = CreateCatletSpecificationSagaState.Initiated;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Name = message.Name;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);
        Data.Data.ConfigYaml = message.ConfigYaml;
        Data.Data.Comment = message.Comment;
        Data.Data.SpecificationId = Guid.NewGuid();
        Data.Data.ProjectId = message.ProjectId;

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = message.ConfigYaml,
            Architecture = Data.Data.Architecture,
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSpecificationSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = CreateCatletSpecificationSagaState.SpecificationBuilt;

            Data.Data.ResolvedGenes = response.ResolvedGenes;
            Data.Data.BuiltConfig = response.BuiltConfig;

            var specificationVersion = new CatletSpecificationVersion
            {
                Id = Guid.NewGuid(),
                ConfigYaml = Data.Data.ConfigYaml!,
                Comment = Data.Data.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var specification = new CatletSpecification
            {
                Id = Data.Data.SpecificationId,
                ProjectId = Data.Data.ProjectId,
                Environment = EryphConstants.DefaultEnvironmentName,
                Name = Data.Data.Name!,
                Architecture = Data.Data.Architecture!.Value,
                Versions = [specificationVersion]
            };

            await stateStore.For<CatletSpecification>().AddAsync(specification);
            await stateStore.SaveChangesAsync();

            await Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
