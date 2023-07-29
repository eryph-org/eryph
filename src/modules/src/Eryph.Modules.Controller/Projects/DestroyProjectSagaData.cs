using System;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Projects
{
    public class DestroyProjectSagaData : TaskWorkflowSagaData
    {
        public Guid ProjectId { get; set; }
    }
}