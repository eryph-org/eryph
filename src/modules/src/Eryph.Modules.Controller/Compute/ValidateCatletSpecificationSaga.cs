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
    IStorageManagementAgentLocator agentLocator,
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
            // PendingArchitectures can deserialize as null for a saga instance persisted before it
            // was set (guard as UpdateCatletSpecificationSaga does).
            Data.Data.PendingArchitectures = toHashSet(Data.Data.PendingArchitectures ?? new HashSet<Architecture>())
                .Remove(architecture)
                .ToHashSet();

            // Fall back to the first built architecture for a saga instance started before the
            // primary was chosen up front (backward compatibility), so the config/genes are always
            // captured for one architecture.
            Data.Data.PrimaryArchitecture ??= architecture;

            // Return the built config/genes of the deterministic primary architecture only, so the
            // response is stable regardless of build completion order. BuiltConfig is required by
            // the API result mapping, so treat it as required here (matching create/update).
            if (Data.Data.PrimaryArchitecture is { } primaryArchitecture
                && architecture.Equals(primaryArchitecture))
            {
                Data.Data.BuiltConfig = response.BuiltConfig ?? throw new InvalidOperationException(
                    "BuiltConfig is required");
                Data.Data.ResolvedGenes = response.ResolvedGenes ?? throw new InvalidOperationException(
                    "ResolvedGenes is required");
            }

            // Wait until every requested architecture has built successfully.
            if (Data.Data.PendingArchitectures.Count > 0)
                return;

            // The primary architecture's result must have been captured; guard rather than complete
            // a "valid" response with a null payload that the result mapping would then reject.
            if (Data.Data.BuiltConfig is null || Data.Data.ResolvedGenes is null)
            {
                await Fail("The specification build did not produce a configuration.");
                return;
            }

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
        var defaultArchitecture = Architecture.New(EryphConstants.DefaultArchitecture);
        var architectures = message.Architectures is { Count: > 0 }
            ? message.Architectures
            : new HashSet<Architecture> { defaultArchitecture };
        Data.Data.PendingArchitectures = architectures;

        // The response can only carry one built config/genes; pick a deterministic architecture for
        // it — the default when requested, otherwise the first by ordinal — so it is stable.
        Data.Data.PrimaryArchitecture = architectures.Contains(defaultArchitecture)
            ? defaultArchitecture
            : architectures.OrderBy(a => a.Value, StringComparer.Ordinal).First();

        foreach (var architecture in architectures)
            await StartNewTask(new BuildCatletSpecificationCommand
            {
                ContentType = message.ContentType,
                Configuration = message.Configuration,
                Architecture = architecture,
                // Resolve genes on a registered host agent's gene pool (see
                // CreateCatletSpecificationSaga): the controller's own machine name has no gene pool
                // queue in the split runtime.
                AgentName = agentLocator.FindAgentForGenePool(),
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
