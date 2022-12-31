using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Modules.Controller.Operations;

namespace Eryph.Modules.Controller.Networks
{
    public class CreateProjectNetworksSagaData : TaskWorkflowSagaData
    {
        public ProjectNetworksConfig? Config { get; set; }
    }
}