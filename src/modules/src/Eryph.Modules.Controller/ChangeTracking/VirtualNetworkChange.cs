using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class VirtualNetworkChange
{
    public List<Guid> ProjectIds { get; set; } = new();
}
