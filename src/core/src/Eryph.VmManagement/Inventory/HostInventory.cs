using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Eryph.VmManagement.Inventory;

internal class HostInventory
{
    private readonly IPowershellEngine _engine;
    private readonly ILogger _log;
    private readonly Guid _standardSwitchId = Guid.Parse("c08cb7b8-9b3c-408e-8e30-5e16a3aeb444");

    public HostInventory(IPowershellEngine engine, ILogger log)
    {
        _engine = engine;
        _log = log;
    }

    public async Task<Either<PowershellFailure, VMHostMachineData>> InventorizeHost()
    {

        var res = await (
            from switches in _engine.GetObjectsAsync<dynamic>(new PsCommandBuilder().AddCommand("get-VMSwitch"))
                .ToAsync()
            from switchNames in Prelude
                .Right<PowershellFailure, string[]>(switches.Select(s => (string)s.Value.Name).ToArray()).ToAsync()
            from adapters in _engine.GetObjectsAsync<VMNetworkAdapter>(new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter")
                .AddParameter("ManagementOS")).ToAsync()
            let standardSwitchAdapter = FindStandardSwitchAdapter(adapters)
            let virtualNetworks = adapters.Map(a => 
                    GetVirtualNetworkFromAdapter(a,IsStandardSwitchAdapter(a.Value.Id, standardSwitchAdapter)))
                .Map(o => o.AsEnumerable()).Flatten()
            let hostNetworks = GetAllHostNetworks(virtualNetworks, standardSwitchAdapter)
            select new VMHostMachineData
            {
                Name = Environment.MachineName,
                Switches = switches.Select(s => new VMHostSwitchData
                {
                    Id = s.Value.Id.ToString(),
                    VirtualSwitchName = s.Value.Name.ToString()
                }).ToArray(),
                VirtualNetworks = virtualNetworks.ToArray(),
                Networks = hostNetworks.ToArray(),
                HardwareId = GetHostUuid() ?? GetHostMachineGuid()
            }).ToEither();

        return res;
    }

    private IEnumerable<MachineNetworkData> GetAllHostNetworks(IEnumerable<HostVirtualNetworkData> virtualNetworks, Option<TypedPsObject<VMNetworkAdapter>> standardSwitchAdapter)
    {
        return NetworkInterface.GetAllNetworkInterfaces().Map(nwInterface =>
        {
            var (name, isStandardSwitch) =
                virtualNetworks.Find(virtualNetwork => virtualNetwork.DeviceId == nwInterface.Id)
                    .Match(virtualNetwork => (virtualNetwork.Name,
                            IsStandardSwitchAdapter(virtualNetwork.AdapterId, standardSwitchAdapter)),
                        () => (nwInterface.Name, false) );

            return NetworkInterfaceToMachineNetworkData<MachineNetworkData>(nwInterface, isStandardSwitch, name);
        });
    }


    private Option<HostVirtualNetworkData> GetVirtualNetworkFromAdapter(
        TypedPsObject<VMNetworkAdapter> adapterInfo, bool isStandardSwitchAdapter)
    {
        if (adapterInfo.Value.SwitchId == _standardSwitchId && !isStandardSwitchAdapter)
            return Option<HostVirtualNetworkData>.None;

        //TODO: needs abstraction for testing
        var networkInfo = from nwInterface in NetworkInterface.GetAllNetworkInterfaces()
                .Find(x => x.Id == adapterInfo.Value.DeviceId)
                .Map(Option<NetworkInterface>.Some)
                .IfNone(() =>
                {
                    _log.LogWarning(
                        $"Could not find host network adapter for switch '{adapterInfo.Value.SwitchName}', VM Adapter: '{adapterInfo.Value.Name}').");
                    return Option<NetworkInterface>.None;
                })
            let networkName = isStandardSwitchAdapter 
                ? "nat" 
                : adapterInfo.Value.SwitchName.Apply(s => Regex.Match(s, @"^([\w\-]+)")).Value
            let network =
                NetworkInterfaceToMachineNetworkData<HostVirtualNetworkData>(nwInterface, isStandardSwitchAdapter,
                    networkName)
            select network.Apply(n =>
            {
                n.VirtualSwitchId = adapterInfo.Value.SwitchId;
                n.DeviceId = adapterInfo.Value.DeviceId;
                n.AdapterId = adapterInfo.Value.Id;
                return n;
            });   
                   

        return networkInfo;
    }


    private Option<TypedPsObject<VMNetworkAdapter>> FindStandardSwitchAdapter(IEnumerable<TypedPsObject<VMNetworkAdapter>> adapters)
    {
        var swAdapters = adapters.Where(a => a.Value.SwitchId == _standardSwitchId).ToList();

        if (swAdapters.Length() > 1)
        {
            var adapterNames = string.Join(',', swAdapters.Select(x => x.Value.Name));
            _log.LogDebug("Multiple candidates found for standard switch port. Port candidates: {adapterNames}", adapterNames);

            // this is a dirty hack to filter out ports from Sandbox / Windows Containers
            // at least for Sandbox additional ports are on the standard switch that have to be ignored
            // these ports all have additional properties, so we look for a port without additional properties in registry

            //TODO: needs abstraction for testing
            foreach (var adapter in swAdapters.ToArray())
            {
                var portPath = adapter.Value.Id.Replace("Microsoft:", "");
                var subKeys =
                    Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\vmsmp\parameters\SwitchList\{portPath}")?.GetSubKeyNames();

                var adapterName = adapter.Value.Name;

                if (subKeys is not { Length: > 0 }) continue;

                _log.LogTrace("Standard switch candidate '{adapterName}' has additional port properties - ignored", adapterName);
                swAdapters.Remove(adapter);
            }
        }

        if (swAdapters.Length() > 1)
        {
            var adapterNames = string.Join(',', swAdapters.Select(x => x.Value.Name));
            _log.LogWarning("Multiple candidates found for standard switch port. Choosing first port. Port candidates: {adapterNames}", adapterNames);

        }

        return swAdapters.HeadOrNone();
    }

    private static bool IsStandardSwitchAdapter(string adapterId,
        Option<TypedPsObject<VMNetworkAdapter>> standardSwitchAdapter)
    {
        return standardSwitchAdapter.Map(sa => adapterId == sa.Value.Id)
            .Match(s => s, () => false);

    }

    private static T NetworkInterfaceToMachineNetworkData<T>(NetworkInterface networkInterface,
        bool isStandardSwitchAdapter, string name) where T: MachineNetworkData, new()
    {
        var networks = networkInterface.GetIPProperties().UnicastAddresses.Select(x =>
            IPNetwork.Parse(x.Address.ToString(), (byte)x.PrefixLength)).ToArray();

        return new T
        {
            DefaultGateways = isStandardSwitchAdapter
                ? networkInterface.GetIPProperties().UnicastAddresses.Select(x => x.Address.ToString()).ToArray()
                : networkInterface.GetIPProperties().GatewayAddresses.Select(x => x.Address.ToString()).ToArray(),
            DhcpEnabled =
                isStandardSwitchAdapter || networkInterface.GetIPProperties().GetIPv4Properties().IsDhcpEnabled,
            DnsServers = isStandardSwitchAdapter
                ? networkInterface.GetIPProperties().UnicastAddresses.Select(x => x.Address.ToString()).ToArray()
                : networkInterface.GetIPProperties().DnsAddresses.Select(x => x.ToString()).ToArray(),
            IPAddresses =
                networkInterface.GetIPProperties().UnicastAddresses.Select(x => x.Address.ToString()).ToArray(),
            Name = name
                .ToLowerInvariant(),
            Subnets = networks.Select(x => x.ToString()).ToArray()

        };
    }


    private static string GetHostUuid()
    {
        var uiid = "";
        try
        {
            var uuidSearcher = new ManagementObjectSearcher("SELECT UUId FROM Win32_ComputerSystemProduct");
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
            using (var registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                if (registryKey != null) guid = registryKey.GetValue("MachineGuid") as string;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return guid;
    }
}