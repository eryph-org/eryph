using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Threading;

namespace Eryph.DistributedLock;

public sealed class DistributedLockScopeHolder(
    IDistributedLockProvider lockProvider)
    : IDistributedLockScopeHolder
{
    /// <summary>
    /// Contains the handles for the locks which have been acquired.
    /// </summary>
    /// <remarks>
    /// The handles are stored in a stack to ensure that they are released
    /// in the reverse order of acquisition.
    /// </remarks>
    private readonly ConcurrentStack<IDistributedSynchronizationHandle> _locks = new();

    /// <summary>
    /// Contains the names of the locks which have been acquired.
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _lockNames = new();
    private readonly SemaphoreSlim _semaphore = new(1);
    private int _disposed;

    // The locks of this manager protect updates ot the state database.
    // If such an update takes longer than 3 minutes, something is severely wrong.
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public async ValueTask AcquireLock(string name, TimeSpan timeOut)
    {
        if (_lockNames.TryGetValue(name, out _))
            return;

        if (!await _semaphore.WaitAsync(timeOut))
            throw new TimeoutException();
        try
        {
            if (_lockNames.TryGetValue(name, out _))
                return;

            var syncHandle = await lockProvider.AcquireLockAsync(name, timeOut);
            _locks.Push(syncHandle);
            _lockNames.TryAdd(name, name);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        while (_locks.TryPop(out var syncHandle))
        {
            syncHandle.Dispose();
        }

        _semaphore.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        while (_locks.TryPop(out var syncHandle))
        {
            await syncHandle.DisposeAsync();
        }

        _semaphore.Dispose();
    }
}