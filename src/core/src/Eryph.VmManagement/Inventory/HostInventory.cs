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

internal class HostInventory
{
    private readonly ILogger _log;
    private readonly INetworkProviderManager _networkProviderManager;

    public HostInventory( ILogger log, INetworkProviderManager networkProviderManager)
    {
        _log = log;
        _networkProviderManager = networkProviderManager;
    }

    public Task<Either<Error, VMHostMachineData>> InventorizeHost()
    {

        var res = 
            (from providerConfig in _networkProviderManager.GetCurrentConfiguration()
            select new VMHostMachineData
            {
                Name = Environment.MachineName,
                HardwareId = GetHostUuid() ?? GetHostMachineGuid(),
                NetworkProviderConfiguration = providerConfig
            }).ToEither();

        return res;
    }


    private static string GetHostUuid()
    {
        var uiid = "";
        try
        {
            using var uuidSearcher = new ManagementObjectSearcher("SELECT UUId FROM Win32_ComputerSystemProduct");
            foreach (var uuidSearcherResult in uuidSearcher.Get())
            {
                uiid = uuidSearcherResult["UUId"] as string;

                if (uiid == "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                    uiid = null;
                break;
            }

        }
        catch (Exception)
        {
            // ignored
        }

        return uiid;
    }

    private static string GetHostMachineGuid()
    {
        var guid = "";
        try
        {
            using var registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            if (registryKey != null) guid = registryKey.GetValue("MachineGuid") as string;
        }
        catch (Exception)
        {
            // ignored
        }

        return guid;
    }
}