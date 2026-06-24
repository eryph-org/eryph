using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class PopulateCatletConfigVariablesSaga(
    IBus bus,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<PopulateCatletConfigVariablesCommand,
            EryphSagaData<PopulateCatletConfigVariablesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message) =>
        FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            var config = Data.Data.Config ?? throw new InvalidOperationException("CatletConfig must not be null in PopulateCatletConfigVariablesSaga.");
            await Complete(new PopulateCatletConfigVariablesCommandResponse
            {
                Config = config.CloneWith(c => { c.Variables = (response.BuiltConfig ?? throw new InvalidOperationException(
                    "The response is missing the built config.")).Variables; }),
            });
        });

    protected override async Task Initiated(PopulateCatletConfigVariablesCommand message)
    {
        var config = message.Config ?? throw new InvalidOperationException("CatletConfig must not be null in PopulateCatletConfigVariablesSaga.");

        Data.Data.Config = config;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ContentType = "application/yaml",
            Configuration = CatletConfigYamlSerializer.Serialize(
                config.CloneWith(c => { c.Project = null; })),
            Architecture = Architecture.New(EryphConstants.DefaultArchitecture),
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<PopulateCatletConfigVariablesSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
