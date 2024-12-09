using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Threading;

namespace Eryph.DistributedLock;

/// <inheritdoc cref="IDistributedLockScopeHolder"/>
/// <remarks>
/// Normally, this class should only be used by a single thread.
/// However, the implementation is thread-safe to ensure that
/// all locks are released even if the class is used by
/// multiple threads.
/// </remarks>
public sealed class DistributedLockScopeHolder(
    IDistributedLockProvider lockProvider)
    : IDistributedLockScopeHolder
{
    /// <summary>
    /// Contains the handles of the locks which have been acquired.
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