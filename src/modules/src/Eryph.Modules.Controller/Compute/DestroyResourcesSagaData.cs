using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute;

public class DestroyResourcesSagaData : TaskWorkflowSagaData
{
    public DestroyResourceState State { get; set; }

    public Resource[]? Resources { get; set; }

    public IReadOnlyList<Guid> PendingCatlets { get; set; } = [];

    public IReadOnlyList<Guid> PendingDisks { get; set;  } = [];

    public IReadOnlyList<Guid> PendingNetworks { get; set; } = [];

    public IReadOnlyList<Resource> DestroyedResources { get; set; } = [];
    
    public IReadOnlyList<Resource> DetachedResources { get; set; } = [];

    public List<List<Resource>> DestroyGroups { get; set; }
}
