using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Compute;

internal class ResolveCatletParentSagaData : TaskWorkflowSagaData
{
    public ResolveCatletParentSagaState State { get; set; }

    public string AgentName { get; set; }

    public string ParentId { get; set; }

    public CatletConfig ParentConfig { get; set; }
}

internal enum ResolveCatletParentSagaState
{
    Inititiated = 0,
    CatletGeneFetched = 10,
}