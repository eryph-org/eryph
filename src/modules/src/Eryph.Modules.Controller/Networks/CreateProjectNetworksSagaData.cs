using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Networks;

namespace Eryph.Modules.Controller.Networks;

public class CreateProjectNetworksSagaData : TaskWorkflowSagaData
{
    public ProjectNetworksConfig? Config { get; set; }
}
