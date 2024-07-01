using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Compute;

internal class BreedCatletSagaData : TaskWorkflowSagaData
{
    public string AgentName { get; set; }

    public CatletConfig? ParentConfig { get; set; }

    public CatletConfig? BreedConfig { get; set; }

    public CatletConfig? CatletConfig { get; set; }

    public CatletConfig? ResolvedConfig { get; set; }

    public IDictionary<string, CatletConfig> ResolvedParents { get; set; } = new Dictionary<string, CatletConfig>();
}

internal enum BreedCatletSagaState
{
    Initiated = 0,
    GenesInConfigResolved = 10,
    ParentsResolved = 20,
}
