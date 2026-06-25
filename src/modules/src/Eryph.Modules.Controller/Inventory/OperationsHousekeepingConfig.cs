using System;

namespace Eryph.Modules.Controller.Inventory;

internal class OperationsHousekeepingConfig
{
    /// <summary>
    /// Operations which have not been updated for longer than this are deleted.
    /// </summary>
    public TimeSpan RetentionAge { get; init; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Operations which are still queued or running but have not been updated
    /// for longer than this are marked as failed.
    /// </summary>
    public TimeSpan RunningTimeout { get; init; } = TimeSpan.FromDays(1);
}
