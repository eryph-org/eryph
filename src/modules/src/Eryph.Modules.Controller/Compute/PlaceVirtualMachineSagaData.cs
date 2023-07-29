using System;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Compute
{
    public class PlaceVirtualCatletSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public CatletConfig? Config { get; set; }
    }
}