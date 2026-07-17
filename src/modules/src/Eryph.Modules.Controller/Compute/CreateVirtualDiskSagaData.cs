using System;

namespace Eryph.Modules.Controller.Compute;

internal class CreateVirtualDiskSagaData
{
    public string? AgentName { get; set; }

    public Guid DiskId { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>The site realizing the disk's environment, resolved when the saga starts.</summary>
    public Guid SiteId { get; set; }
}
