using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.Core.Genetics;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class UpdateCatletSpecificationSaga(
    IStateStore stateStore,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<UpdateCatletSpecificationCommand, EryphSagaData<UpdateCatletSpecificationSagaData>>(
            workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= UpdateCatletSpecificationSagaState.SpecificationBuilt)
            return Task.CompletedTask;

        return FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            var architecture = response.Architecture ?? throw new InvalidOperationException(
                "The build response is missing the architecture.");
            Data.Data.PendingArchitectures = toHashSet(Data.Data.PendingArchitectures ?? new HashSet<Architecture>())
                .Remove(architecture)
                .ToHashSet();

            var variantData = new CatletSpecificationVersionVariantSagaData
            {
                Architecture = architecture,
                BuiltConfig = response.BuiltConfig ?? throw new InvalidOperationException("BuiltConfig is required"),
                ResolvedGenes = response.ResolvedGenes ?? throw new InvalidOperationException("ResolvedGenes is required"),
            };

            Data.Data.Variants = Data.Data.Variants
                .ToHashMap()
                .AddOrUpdate(architecture, variantData)
                .ToDictionary();

            if (Data.Data.PendingArchitectures.Count > 0)
                return;

            Data.Data.State = UpdateCatletSpecificationSagaState.SpecificationBuilt;

            var specification = await stateStore.For<CatletSpecification>()
                .GetByIdAsync(Data.Data.SpecificationId);
            if (specification is null)
            {
                await Fail($"The catlet specification {Data.Data.SpecificationId} does not exist.");
                return;
            }

            specification.Name = Data.Data.Variants.Values.First().BuiltConfig.Name!;
            specification.Architectures = Data.Data.Variants.Keys.ToHashSet();

            var specificationVersion = new CatletSpecificationVersion
            {
                Id = Data.Data.SpecificationVersionId,
                SpecificationId = Data.Data.SpecificationId,
                ContentType = Data.Data.ContentType!,
                Configuration = Data.Data.OriginalConfig!.ReplaceLineEndings("\n"),
                Comment = Data.Data.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
                Architectures = Data.Data.Variants.Keys.ToHashSet(),
                Variants = Data.Data.Variants.Values
                    .Map(v => v.ToDbVariant(Data.Data.SpecificationVersionId))
                    .ToList(),
            };

            await stateStore.For<CatletSpecificationVersion>().AddAsync(specificationVersion);
            await stateStore.SaveChangesAsync();

            await Complete();
        });
    }

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
        Data.Data.ContentType = message.ContentType;
        Data.Data.OriginalConfig = message.Configuration;
        Data.Data.Architectures = message.Architectures ?? throw new InvalidOperationException("Architectures is required");
        Data.Data.Comment = message.Comment;
        Data.Data.PendingArchitectures = message.Architectures ?? throw new InvalidOperationException("Architectures is required");

        foreach (var architecture in Data.Data.PendingArchitectures)
            await StartNewTask(new BuildCatletSpecificationCommand
            {
                AgentName = Data.Data.AgentName,
                ContentType = message.ContentType,
                Configuration = message.Configuration,
                Architecture = architecture,
            });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<UpdateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
