using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class DestroyResourcesSagaData : TaskWorkflowSagaData
    {
        public DestroyResourceState State { get; set; }

        public Resource[]? Resources { get; set; }
        public List<Resource> DestroyedResources { get; set; } = new();
        public List<Resource> DetachedResources { get; set; } = new();
    }
}