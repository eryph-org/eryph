using System;
using System.Collections.Generic;
using System.Linq;
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

using static LanguageExt.Prelude;

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
        Data.Data.Architectures = message.Architectures;
        Data.Data.ContentType = message.ContentType;
        Data.Data.Configuration = message.Configuration;
        Data.Data.Comment = message.Comment;
        Data.Data.SpecificationId = Guid.NewGuid();
        Data.Data.SpecificationVersionId = Guid.NewGuid();
        Data.Data.ProjectId = message.ProjectId;
        Data.Data.PendingArchitectures = message.Architectures;

        foreach (var architecture in Data.Data.PendingArchitectures)
        {
            await StartNewTask(new BuildCatletSpecificationCommand
            {
                AgentName = Data.Data.AgentName,
                ContentType = Data.Data.ContentType,
                Configuration = message.Configuration,
                Architecture = architecture,
            });
        }
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= CreateCatletSpecificationSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.PendingArchitectures = toHashSet(Data.Data.PendingArchitectures)
                .Remove(response.Architecture)
                .ToHashSet();

            var variantData = new CatletSpecificationVersionVariantSagaData
            {
                Architecture = response.Architecture,
                BuiltConfig = response.BuiltConfig,
                ResolvedGenes = response.ResolvedGenes,
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
                // TODO validate that catlet name is specified
                Name = response.BuiltConfig.Name!,
                Architectures = Data.Data.Architectures!,
                Versions = [specificationVersion]
            };

            await repository.AddAsync(specification);
            await repository.SaveChangesAsync();

            await Complete(new ResourceReference
            {
                Resource = new Resources.Resource(ResourceType.CatletSpecification, Data.Data.SpecificationId),
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
