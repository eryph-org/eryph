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

    public string ParentIdentifier { get; set; }

    public CatletConfig? ParentConfig { get; set; }

    public CatletConfig? BreedConfig { get; set; }

    public CatletConfig? CatletConfig { get; set; }
}