using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    public class PlaceVirtualCatletSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public CatletConfig? Config { get; set; }
    }
}