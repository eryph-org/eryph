using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class ValidateCatletSpecificationSaga(
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ValidateCatletSpecificationCommand, EryphSagaData<ValidateCatletSpecificationSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    protected override async Task Initiated(ValidateCatletSpecificationCommand message)
    {
        Data.Data.State = ValidateCatletSpecificationSagaState.Initiated;
        Data.Data.ConfigYaml = message.ConfigYaml;

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            ConfigYaml = message.ConfigYaml,
            Architecture = Architecture.New(EryphConstants.DefaultArchitecture),
            AgentName = System.Environment.MachineName,
        });
    }

    public async Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= ValidateCatletSpecificationSagaState.SpecificationBuilt)
            return;

        await FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            Data.Data.State = ValidateCatletSpecificationSagaState.SpecificationBuilt;
            Data.Data.BuiltConfig = response.BuiltConfig;
            Data.Data.ResolvedGenes = response.ResolvedGenes;

            await Complete(new ValidateCatletSpecificationCommandResponse
            {
                IsValid = true,
                BuiltConfig = response.BuiltConfig,
                ResolvedGenes = response.ResolvedGenes,
            });
        });
    }

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<ValidateCatletSpecificationSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
