using System;
using Dbosoft.Rebus.Operations.Workflow;

namespace Eryph.Modules.Controller.Networks
{
    public class DestroyVirtualNetworksSagaData : TaskWorkflowSagaData
    {
        public Guid[]? DestroyedNetworks { get; set; }

    }
}