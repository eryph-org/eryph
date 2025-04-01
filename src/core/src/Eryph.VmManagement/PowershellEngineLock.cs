using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.VmManagement;

/// <summary>
/// Provides a global lock for the <see cref="PowershellEngine"/>.
/// </summary>
/// <remarks>
/// Many Hyper-V powershell commands cannot really be run in parallel. For example,
/// <c>Get-VHD</c> can fail with a file-in-use error when another command is accessing
/// the VHD at the same time. For now, we use this global lock to prevent the parallel
/// execution of Powershell commands (unless the caller explicitly opts out of the locking).
/// In the future, this could be optimized with locking per resource.
/// </remarks>
public sealed class PowershellEngineLock : IPowershellEngineLock, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
    }

    public void ReleaseLock()
    {
        _semaphore.Release();
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
