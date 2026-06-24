using System;
using Dbosoft.Rebus.Operations.Workflow;

namespace Eryph.Modules.Controller.Projects;

public class DestroyProjectSagaData : TaskWorkflowSagaData
{
    public Guid ProjectId { get; set; }
}
