using Dbosoft.Rebus.Operations.Workflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

internal class CreateVirtualDiskSagaData : TaskWorkflowSagaData
{
    public string AgentName { get; set; }

    public Guid DiskId { get; set; }

    public Guid ProjectId { get; set; }
}
