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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class PopulateCatletConfigVariablesSaga(
    IBus bus,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<PopulateCatletConfigVariablesCommand, EryphSagaData<PopulateCatletConfigVariablesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>
{
    protected override async Task Initiated(PopulateCatletConfigVariablesCommand message)
    {
        Data.Data.Config = message.Config;
        Data.Data.AgentName = Environment.MachineName;
        Data.Data.Architecture = Architecture.New(EryphConstants.DefaultArchitecture);

        await StartNewTask(new BuildCatletSpecificationCommand
        {
            AgentName = Data.Data.AgentName,
            ConfigYaml = CatletConfigYamlSerializer.Serialize(message.Config),
            Architecture = Architecture.New(EryphConstants.DefaultArchitecture),
        });
    }

    public Task Handle(OperationTaskStatusEvent<BuildCatletSpecificationCommand> message) =>
        FailOrRun(message, async (BuildCatletSpecificationCommandResponse response) =>
        {
            await Complete(new PopulateCatletConfigVariablesCommandResponse
            {
                Config = Data.Data.Config!.CloneWith(c =>
                {
                    c.Variables = response.BuiltConfig.Variables;
                }),
            });
        });

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<PopulateCatletConfigVariablesSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<BuildCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
