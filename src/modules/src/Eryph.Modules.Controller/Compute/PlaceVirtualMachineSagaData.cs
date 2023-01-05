using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Compute
{
    public class PlaceVirtualCatletSagaData : TaskWorkflowSagaData
    {
        public Guid CorrelationId { get; set; }
        public CatletConfig? Config { get; set; }
    }
}