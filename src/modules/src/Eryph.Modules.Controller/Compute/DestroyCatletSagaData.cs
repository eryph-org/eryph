using System;
using System.Collections;
using System.Collections.Generic;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute;

public class DestroyCatletSagaData
{
    public Guid MachineId { get; set; }

    public Guid VmId { get; set; }

    public IList<Resource> DestroyedResources { get; set; } = [];

    public IList<Resource> DetachedResources { get; set; } = [];
}
