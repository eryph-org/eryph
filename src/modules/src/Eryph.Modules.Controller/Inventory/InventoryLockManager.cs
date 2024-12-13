using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.DistributedLock;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Inventory;

internal interface IInventoryLockManager
{
    /// <summary>
    /// Acquires a lock for the given <paramref name="diskIdentifier"/>.
    /// The <paramref name="diskIdentifier"/> is the Hyper-V disk
    /// identifier of the VHD.
    /// </summary>
    /// <remarks>
    /// The Hyper-V disk identifier might not be unique in some rare
    /// cases (e.g. when copying the VHD without resetting the disk
    /// identifier) but it is our best option to uniquely identify
    /// a VHD. Eryph itself always assigns a new disk identifier
    /// when creating a catlet disk (both differential and full copies).
    /// </remarks>
    public ValueTask AcquireVhdLock(Guid diskIdentifier);

    /// <summary>
    /// Acquires a lock for the given <paramref name="vmId"/>.
    /// The <paramref name="vmId"/> is the Hyper-V ID of the VM.
    /// </summary>
    public ValueTask AcquireVmLock(Guid vmId);
}

internal class InventoryLockManager(
    IDistributedLockScopeHolder lockHolder,
    ILogger logger)
    : IInventoryLockManager
{
    // The locks of this manager protect updates to the state database.
    // If such an update takes longer than 3 minutes, something is severely wrong.
    private static readonly TimeSpan TimeOut = TimeSpan.FromMinutes(3);

    public async ValueTask AcquireVhdLock(Guid diskIdentifier)
    {
        await AcquireLock($"vhd-{diskIdentifier}");
    }

    public async ValueTask AcquireVmLock(Guid vmId)
    {
        await AcquireLock($"vm-{vmId}");
    }

    private async ValueTask AcquireLock(string name)
    {
        logger.LogTrace("Acquiring distributed lock '{Name}'", name);
        await lockHolder.AcquireLock(name, TimeOut);
    }
}