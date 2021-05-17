using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Haipa.Primitives;
using Haipa.Primitives.Resources;

namespace Haipa.Modules.Controller.Operations.Workflows
{
    public class DestroyResourcesSagaData : TaskWorkflowSagaData
    {
        public DestroyResourceState State { get; set; }

        public Resource[]? Resources { get; set; }
        public List<Resource> DestroyedResources { get; set; } = new List<Resource>();
        public List<Resource> DetachedResources { get; set; } = new List<Resource>();

    }

    public enum DestroyResourceState
    {
        Initiated = 0,
        ResourcesDestroyed = 5,
        ResourcesReleased = 10,
    }



}