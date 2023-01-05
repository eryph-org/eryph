using Eryph.Modules.Controller.Operations;
using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.Networks;

public class UpdateNetworksSagaData : TaskWorkflowSagaData
{
    public List<Guid>? ProjectsCompleted;
    public Guid[]? ProjectIds;

}