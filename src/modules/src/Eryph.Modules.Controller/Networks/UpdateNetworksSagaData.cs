using Eryph.Modules.Controller.Operations;
using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Events;

namespace Eryph.Modules.Controller.Networks;

public class UpdateNetworksSagaData : TaskWorkflowSagaData
{
    public List<Guid>? ProjectsCompleted { get; set; }
    public Guid[]? ProjectIds { get; set; }
    public List<NetworkNeighborRecord>? UpdatedAddresses { get; set; }

}