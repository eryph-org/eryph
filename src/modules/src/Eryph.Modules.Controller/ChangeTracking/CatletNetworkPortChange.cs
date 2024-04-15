using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking;

internal class CatletNetworkPortChange
{
    public List<Guid> ProjectIds { get; set; } = new();
}
