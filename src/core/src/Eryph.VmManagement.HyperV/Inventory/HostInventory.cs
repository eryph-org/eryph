using System;
using System.Management;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Resources.Machines;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Eryph.VmManagement.Inventory;

internal class HostInventory(
    ILogger log,
    INetworkProviderManager networkProviderManager,
    IHardwareIdProvider hardwareIdProvider)
{
    private readonly ILogger _log = log;

    public Task<Either<Error, VMHostMachineData>> InventorizeHost()
    {

        var res = 
            (from providerConfig in networkProviderManager.GetCurrentConfiguration()
            select new VMHostMachineData
            {
                Name = Environment.MachineName,
                HardwareId = hardwareIdProvider.HardwareId.ToString(),
                NetworkProviderConfiguration = providerConfig
            }).ToEither();

        return res;
    }
}
