using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute;

public class DestroyResourcesSagaData : TaskWorkflowSagaData
{
    public DestroyResourceState State { get; set; }

    public ISet<Guid> PendingCatlets { get; set; } = new HashSet<Guid>();

    public ISet<Guid> PendingDisks { get; set;  } = new HashSet<Guid>();

    public ISet<Guid> PendingNetworks { get; set; } = new HashSet<Guid>();

    public ISet<Guid> PendingCatletSpecifications { get; set; } = new HashSet<Guid>();

    public ISet<Resource> DestroyedResources { get; set; } = new HashSet<Resource>();
    
    public ISet<Resource> DetachedResources { get; set; } = new HashSet<Resource>();
}
