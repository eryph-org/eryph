using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Resources;
using Eryph.StateDb.Model;
using Eryph.ModuleCore;
using Eryph.StateDb;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSpecificationSaga(
    IStateStoreRepository<CatletSpecification> repository,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CreateCatletSpecificationCommand, EryphSagaData<CreateCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{

    protected override async Task Initiated(CreateCatletSpecificationCommand message)
    {
        Data.Data.State = CreateCatletSpecificationSagaState.Initiated;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);
        Data.Data.ContentType = message.ContentType;
        Data.Data.Configuration = message.Configuration;
        Data.Data.Comment = message.Comment;
        Data.Data.SpecificationId = Guid.NewGuid();
        Data.Data.SpecificationVersionId = Guid.NewGuid();
        Data.Data.ProjectId = message.ProjectId;

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            Configuration = message.Configuration,
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
                Id = Data.Data.SpecificationVersionId,
                ContentType = Data.Data.ContentType!,
                Configuration = Data.Data.Configuration!.ReplaceLineEndings("\n"),
                ResolvedConfig = CatletConfigJsonSerializer.Serialize(Data.Data.BuiltConfig!),
                Comment = Data.Data.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
                Genes = Data.Data.ResolvedGenes.ToGenesList(Data.Data.SpecificationVersionId),
            };

            var specification = new CatletSpecification
            {
                Id = Data.Data.SpecificationId,
                ProjectId = Data.Data.ProjectId,
                Environment = EryphConstants.DefaultEnvironmentName,
                // TODO validate that catlet name is specified
                Name = response.BuiltConfig.Name!,
                Architecture = Data.Data.Architecture!.Value,
                Versions = [specificationVersion]
            };

            await repository.AddAsync(specification);
            await repository.SaveChangesAsync();

            await Complete(new ResourceReference
            {
                Resource = new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId),
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CreateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
