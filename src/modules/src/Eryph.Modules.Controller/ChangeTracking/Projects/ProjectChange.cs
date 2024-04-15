using System;
using System.Collections.Generic;

namespace Eryph.Modules.Controller.ChangeTracking.Projects;

internal class ProjectChange
{
    public List<Guid> ProjectIds { get; set; } = new();
}
