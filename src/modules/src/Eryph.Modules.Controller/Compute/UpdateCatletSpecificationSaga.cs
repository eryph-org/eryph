using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Rebus.Handlers;
using Rebus.Sagas;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class UpdateCatletSpecificationSaga(
    IStateStore stateStore,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<UpdateCatletSpecificationCommand, EryphSagaData<UpdateCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    protected override async Task Initiated(UpdateCatletSpecificationCommand message)
    {
        Data.Data.State = UpdateCatletSpecificationSagaState.Initiated;
        Data.Data.SpecificationId = message.SpecificationId;

        var specification = await stateStore.For<CatletSpecification>()
            .GetByIdAsync(message.SpecificationId);
        if (specification is null)
        {
            await Fail($"The catlet specification {message.SpecificationId} does not exist.");
            return;
        }

        Data.Data.SpecificationVersionId = Guid.NewGuid();
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Name = message.Name;
        Data.Data.ConfigYaml = message.ConfigYaml;
        Data.Data.Comment = message.Comment;

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = message.ConfigYaml,
            Architecture = Architecture.New(specification.Architecture),
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= UpdateCatletSpecificationSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = UpdateCatletSpecificationSagaState.SpecificationBuilt;

            Data.Data.ResolvedGenes = response.ResolvedGenes;
            Data.Data.BuiltConfig = response.BuiltConfig;

            var specification = await stateStore.For<CatletSpecification>()
                .GetByIdAsync(Data.Data.SpecificationId);
            if (specification is null)
            {
                await Fail($"The catlet specification {Data.Data.SpecificationId} does not exist.");
                return;
            }

            if (Data.Data.Name is not null)
            {
                specification.Name = Data.Data.Name;
            }
            
            var specificationVersion = new CatletSpecificationVersion
            {
                Id = Data.Data.SpecificationVersionId,
                SpecificationId = Data.Data.SpecificationId,
                ConfigYaml = Data.Data.ConfigYaml!,
                ResolvedConfig = CatletConfigJsonSerializer.Serialize(Data.Data.BuiltConfig!),
                Comment = Data.Data.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
                Genes = Data.Data.ResolvedGenes.ToGenesList(Data.Data.SpecificationVersionId),
            };

            await stateStore.For<CatletSpecificationVersion>().AddAsync(specificationVersion);
            await stateStore.SaveChangesAsync();

            await Complete();
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<UpdateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
