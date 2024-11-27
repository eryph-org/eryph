using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller;

/// <summary>
/// This manager holds locks for a DI scope. The locks are released
/// when the manager is disposed. As each rebus message is handled
/// with its own DI scope, this manager essentially holds the locks
/// until Rebus disposes the message handlers and the scope.
/// Especially, the manager will be disposed after the Rebus unit
/// of work has been committed.
/// </summary>
internal interface IDistributedLockManager
{
    /// <summary>
    /// Acquires a lock for the given <paramref name="vhdId"/>.
    /// The <paramref name="vhdId"/> is the Hyper-V ID of the VHD.
    /// </summary>
    public ValueTask AcquireVhdLock(Guid vhdId);

    /// <summary>
    /// Acquires a lock for the given <paramref name="vmId"/>.
    /// The <paramref name="vmId"/> is the Hyper-V ID of the VM.
    /// </summary>
    public ValueTask AcquireVmLock(Guid vmId);
}

internal sealed class DistributedLockManager(
    IDistributedLockProvider lockProvider,
    ILogger logger)
    : IDistributedLockManager, IDisposable, IAsyncDisposable
{
    // This manager is intended to be scoped per Rebus message.
    // Hence, the manager should not be used by multiple threads.
    // We handle multithreading gracefully just in case.
    //
    // The locks are stored in stack to ensure that they are released
    // in the reverse order of acquisition.
    private readonly ConcurrentStack<IDistributedSynchronizationHandle> _locks = new();
    private int _disposed;

    // The locks of this manager protect updates ot the state database.
    // If such an update takes longer than 3 minutes, something is severely wrong.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public async ValueTask AcquireVhdLock(Guid vhdId)
    {
        await AcquireLock($"vhd-{vhdId}");
    }

    public async ValueTask AcquireVmLock(Guid vmId)
    {
        await AcquireLock($"vm-{vmId}");
    }

    private async ValueTask AcquireLock(string name)
    {
        logger.LogTrace("Acquiring distributed lock '{Name}'", name);
        var syncHandle = await lockProvider.AcquireLockAsync(name, Timeout);
        _locks.Push(syncHandle);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        while (_locks.TryPop(out var syncHandle))
        {
            syncHandle.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        while (_locks.TryPop(out var syncHandle))
        {
            await syncHandle.DisposeAsync();
        }
    }
}
