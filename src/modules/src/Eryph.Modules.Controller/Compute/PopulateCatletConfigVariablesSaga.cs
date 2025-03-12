using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
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
    : OperationTaskWorkflowSaga<PopulateCatletConfigVariablesCommand, EryphSagaData<PopulateCatletConfigVariablesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>
{
    protected override async Task Initiated(PopulateCatletConfigVariablesCommand message)
    {
        Data.Data.Config = message.Config;

        await StartNewTask(new PrepareNewCatletConfigCommand
        {
            Config = message.Config,
        });
    }

    public Task Handle(OperationTaskStatusEvent<PrepareNewCatletConfigCommand> message) =>
        FailOrRun(message, async (PrepareNewCatletConfigCommandResponse response) =>
        {
            await Complete(new PopulateCatletConfigVariablesCommandResponse
            {
                Config = Data.Data.Config!.CloneWith(c =>
                {
                    c.Variables = response.BredConfig.Variables;
                }),
            });
        });

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<PopulateCatletConfigVariablesSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<PrepareNewCatletConfigCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
