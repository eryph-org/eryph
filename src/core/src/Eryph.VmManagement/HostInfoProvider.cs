using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement;

public class HostInfoProvider : IHostInfoProvider
{
    [CanBeNull] private VMHostMachineData _cachedData = null;
    private readonly HostInventory _hostInventory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public HostInfoProvider(ILogger log, INetworkProviderManager networkProviderManager)
    {
        _hostInventory = new HostInventory(log, networkProviderManager);
    }

    public EitherAsync<Error, VMHostMachineData> GetHostInfoAsync(bool refresh = false)
    {
        async Task<Either<Error, VMHostMachineData>> GetHostInfoAsyncAsync()
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

        return GetHostInfoAsyncAsync().ToAsync();
    }
}