using System;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Projects
{
    public class DestroyProjectSagaData : TaskWorkflowSagaData
    {
        public Guid ProjectId { get; set; }
    }
}