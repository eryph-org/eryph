using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualMachines;

internal class CatletMetadataChange
{
    public List<Guid> Ids { get; set; } = new();
}