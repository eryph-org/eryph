using System.Threading;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement;

public class HostInfoProvider : IHostInfoProvider
{
    [CanBeNull] private VMHostMachineData _cachedData = null;
    private readonly HostInventory _hostInventory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public HostInfoProvider(IPowershellEngine engine, ILogger log)
    {
        _hostInventory = new HostInventory(engine, log);
    }

    public async Task<Either<PowershellFailure, VMHostMachineData>> GetHostInfoAsync(bool refresh = false)
    {
        await _lock.WaitAsync();
        try
        {
            if (_cachedData == null || refresh)
            {
                return await _hostInventory.InventorizeHost().MapAsync(r =>
                {
                    _cachedData = r;
                    return r;
                });

            }

            return _cachedData;
        }
        finally
        {
            _lock.Release();
        }
    }
}