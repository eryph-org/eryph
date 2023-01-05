using System.Collections.Generic;
using Eryph.Modules.Controller.Operations;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute
{
    public class DestroyResourcesSagaData : TaskWorkflowSagaData
    {
        public DestroyResourceState State { get; set; }

        public Resource[]? Resources { get; set; }
        public List<Resource> DestroyedResources { get; set; } = new();
        public List<Resource> DetachedResources { get; set; } = new();

        public List<List<Resource>> DestroyGroups { get; set; }
    }
}