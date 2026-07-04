using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ValidateCatletSpecificationSaga(
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ValidateCatletSpecificationCommand, EryphSagaData<ValidateCatletSpecificationSagaData>>(
            workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    public async Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= ValidateCatletSpecificationSagaState.SpecificationBuilt)
            return;

        await FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            var architecture = response.Architecture ?? throw new InvalidOperationException(
                "The build response is missing the architecture.");
            Data.Data.PendingArchitectures = toHashSet(Data.Data.PendingArchitectures)
                .Remove(architecture)
                .ToHashSet();

            // Validation only needs to know the spec builds; keep the last built config/genes as a
            // representative result for the response.
            Data.Data.BuiltConfig = response.BuiltConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            // Wait until every requested architecture has built successfully.
            if (Data.Data.PendingArchitectures.Count > 0)
                return;

            Data.Data.State = ValidateCatletSpecificationSagaState.SpecificationBuilt;

            await Complete(new ValidateCatletSpecificationCommandResponse
            {
                IsValid = true,
                BuiltConfig = Data.Data.BuiltConfig,
                ResolvedGenes = Data.Data.ResolvedGenes,
            });
        });
    }

    protected override async Task Initiated(ValidateCatletSpecificationCommand message)
    {
        Data.Data.State = ValidateCatletSpecificationSagaState.Initiated;
        Data.Data.ConfigYaml = message.Configuration;

        // Validate every requested architecture (falling back to the default when none was given),
        // matching what create/update actually build — so an architecture-specific build failure is
        // caught by validation instead of surfacing only later on save.
        var architectures = message.Architectures is { Count: > 0 }
            ? message.Architectures
            : new HashSet<Architecture> { Architecture.New(EryphConstants.DefaultArchitecture) };
        Data.Data.PendingArchitectures = architectures;

        foreach (var architecture in architectures)
            await StartNewTask(new BuildCatletSpecificationCommand
            {
                ContentType = message.ContentType,
                Configuration = message.Configuration,
                Architecture = architecture,
                AgentName = Environment.MachineName,
            });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<ValidateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
