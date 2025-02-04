using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Json;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;

namespace Eryph.Modules.Controller.Compute;

public class ExpandNewCatletConfigSaga(
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<ExpandNewCatletConfigCommand, EryphSagaData<ExpandNewCatletConfigSagaData>>(workflow)
{
    protected override Task Initiated(
        ExpandNewCatletConfigCommand message)
    {
        return Complete(new ExpandNewCatletConfigCommandResponse
        {
            Config = message.Config,
        });
    }
}
