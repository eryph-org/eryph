using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.Operations.Workflows;

public class UpdateNetworksSagaData : TaskWorkflowSagaData
{
    public List<Guid>? ProjectsCompleted;
    public Guid[]? ProjectIds;

}