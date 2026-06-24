using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using Eryph.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;
using Resource = Eryph.Resources.Resource;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class CreateCatletSpecificationSaga(
    IStateStoreRepository<CatletSpecification> repository,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CreateCatletSpecificationCommand, EryphSagaData<CreateCatletSpecificationSagaData>>(
            workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSpecificationSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.PendingArchitectures = toHashSet(Data.Data.PendingArchitectures)
                .Remove(response.Architecture ?? throw new InvalidOperationException("Architecture cannot be null"))
                .ToHashSet();

            var builtConfig = response.BuiltConfig ?? throw new InvalidOperationException("BuiltConfig cannot be null");
            var resolvedGenes = response.ResolvedGenes ?? throw new InvalidOperationException("ResolvedGenes cannot be null");
            var variantData = new CatletSpecificationVersionVariantSagaData
            {
                Architecture = response.Architecture,
                BuiltConfig = builtConfig,
                ResolvedGenes = resolvedGenes,
            };

            Data.Data.Variants = Data.Data.Variants
                .ToHashMap()
                .AddOrUpdate(response.Architecture, variantData)
                .ToDictionary();

            if (Data.Data.PendingArchitectures.Count > 0)
                return;

            Data.Data.State = CreateCatletSpecificationSagaState.SpecificationBuilt;


            var specificationVersion = new CatletSpecificationVersion
            {
                Id = Data.Data.SpecificationVersionId,
                ContentType = Data.Data.ContentType!,
                Configuration = Data.Data.Configuration!.ReplaceLineEndings("\n"),
                Architectures = Data.Data.Architectures!,
                Comment = Data.Data.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
                Variants = Data.Data.Variants.Values
                    .Map(v => v.ToDbVariant(Data.Data.SpecificationVersionId))
                    .ToList(),
            };

            var specification = new CatletSpecification
            {
                Id = Data.Data.SpecificationId,
                ProjectId = Data.Data.ProjectId,
                Environment = EryphConstants.DefaultEnvironmentName,
                // Normize the name just to be sure. This also makes sure that
                // a name has been provided. We should have validated this earlier though.
                Name = CatletName.New(builtConfig.Name).Value,
                Architectures = Data.Data.Architectures!,
                Versions = [specificationVersion],
            };

            await repository.AddAsync(specification);
            await repository.SaveChangesAsync();

            await Complete(new ResourceReference
            {
                Resource = new Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId),
            });
        });
    }

    protected override async Task Initiated(CreateCatletSpecificationCommand message)
    {
        Data.Data.State = CreateCatletSpecificationSagaState.Initiated;
        Data.Data.AgentName = Environment.MachineName;
        var architectures = message.Architectures ?? throw new InvalidOperationException("Architectures cannot be null");
        Data.Data.Architectures = architectures;
        Data.Data.ContentType = message.ContentType;
        Data.Data.Configuration = message.Configuration;
        Data.Data.Comment = message.Comment;
        Data.Data.SpecificationId = Guid.NewGuid();
        Data.Data.SpecificationVersionId = Guid.NewGuid();
        Data.Data.ProjectId = message.ProjectId;
        Data.Data.PendingArchitectures = architectures;

        foreach (var architecture in architectures)
            await StartNewTask(new BuildCatletSpecificationCommand
            {
                AgentName = Data.Data.AgentName,
                ContentType = Data.Data.ContentType,
                Configuration = message.Configuration,
                Architecture = architecture,
            });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<CreateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
