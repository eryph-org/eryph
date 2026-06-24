using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.Modules.Controller.Compute;

public class DestroyCatletSagaData
{
    public Guid MachineId { get; set; }

    public Guid VmId { get; set; }

    public IList<Resource> DestroyedResources { get; set; } = [];

    public IList<Resource> DetachedResources { get; set; } = [];
}
