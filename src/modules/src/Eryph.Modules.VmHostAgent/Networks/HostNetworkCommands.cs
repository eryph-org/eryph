using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public class HostNetworkCommands<RT> : IHostNetworkCommands<RT>
    where RT : struct, HasPowershell<RT>, HasCancel<RT>
{
    public Aff<RT, Seq<VMSwitch>> GetSwitches() =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create().AddCommand("Get-VMSwitch")
        from vmSwitches in psEngine.GetObjectsAsync<VMSwitch>(command).ToAff()
        from switches in vmSwitches
            .Map(s => Eff(() =>
            {
                // Try to fetch interface IDs
                s.Value.NetAdapterInterfaceGuid = s.GetNetAdapterInterfaceGuid();
                return s.Value;
            })).Sequence()
        select switches;

    public Aff<RT, Option<VMSystemSwitchExtension>> GetInstalledSwitchExtension() =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create().AddCommand("Get-VMSystemSwitchExtension")
        from results in psEngine.GetObjectValuesAsync<VMSystemSwitchExtension>(command).ToAff()
        select results
            .Filter(e => string.Equals(e.Name, EryphConstants.SwitchExtensionName, StringComparison.OrdinalIgnoreCase))
            .HeadOrNone();

    public Aff<RT, Seq<VMSwitchExtension>> GetSwitchExtensions() =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMSwitch")
            .AddCommand("Get-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)
        from vmSwitchExtensions in psEngine.GetObjectValuesAsync<VMSwitchExtension>(command).ToAff()
        select vmSwitchExtensions;

    public Aff<RT, Unit> DisableSwitchExtension(Guid switchId) =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMSwitch")
            .AddParameter("Id", switchId)
            .AddCommand("Get-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)
            .AddCommand("Disable-VMSwitchExtension")
        from _ in psEngine.RunAsync(command).ToAff()
        select unit;

    public Aff<RT, Unit> EnableSwitchExtension(Guid switchId) =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMSwitch")
            .AddParameter("Id", switchId)
            .AddCommand("Enable-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)
        from _ in psEngine.RunAsync(command).ToAff()
        select unit;
    
    public Aff<RT, Seq<HostNetworkAdapter>> GetPhysicalAdapters() =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-NetAdapter")
            .AddParameter("Physical")
        from netAdapters in psEngine.GetObjectValuesAsync<HostNetworkAdapter>(command).ToAff()
        select netAdapters;

    public Aff<RT, Seq<string>> GetAdapterNames() =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-NetAdapter")
        from netAdapters in psEngine.GetObjectValuesAsync<HostNetworkAdapter>(command).ToAff()
        select netAdapters.Map(a => a.Name);

    public Aff<RT, Seq<NetNat>> GetNetNat() =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-NetNat")
        from netNat in psEngine.GetObjectValuesAsync<NetNat>(command).ToAff()
        select netNat;

    public Aff<RT, Unit> EnableBridgeAdapter(string adapterName) =>
        from psEngine in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-NetAdapter")
            .AddArgument(adapterName)
            .AddCommand("Enable-NetAdapter")
        from _ in psEngine.RunAsync(command).ToAff()
        select unit;

    public Aff<RT, Unit> ConfigureAdapterIp(
        string adapterName,
        IPAddress ipAddress,
        IPNetwork2 network) =>
        from psEngine in default(RT).Powershell.ToAff()
        let ipCommand = PsCommandBuilder.Create()
            .AddCommand("New-NetIPAddress")
            .AddParameter("InterfaceAlias", adapterName)
            .AddParameter("IPAddress", ipAddress.ToString())
            .AddParameter("PrefixLength", network.Cidr)
        from uEnable in psEngine.RunAsync(
            PsCommandBuilder.Create()
                .AddCommand("Get-NetAdapter")
                .AddArgument(adapterName)
                .AddCommand("Enable-NetAdapter")).ToAff()
        from uNoDhcp in psEngine.RunAsync(
            PsCommandBuilder.Create()
                .AddCommand("Set-NetIPInterface")
                .AddParameter("InterfaceAlias", adapterName)
                .AddParameter("Dhcp", "Disabled")
                .AddParameter("Confirm", false)).ToAff()
        from uRemoveGateway in psEngine.RunAsync(
            PsCommandBuilder.Create()
                .AddCommand("Remove-NetRoute")
                .AddParameter("InterfaceAlias", adapterName)
                .AddParameter("Confirm", false)
                .AddParameter("ErrorAction", "SilentlyContinue")).ToAff()
        from uRemoveIP in psEngine.RunAsync(
            PsCommandBuilder.Create()
                .AddCommand("Remove-NetIPAddress")
                .AddParameter("InterfaceAlias", adapterName)
                .AddParameter("Confirm", false)
                .AddParameter("ErrorAction", "SilentlyContinue")).ToAff()
        from uAddIp in psEngine.RunAsync(ipCommand).ToAff()
        select unit;

    public Aff<RT, Seq<TypedPsObject<VMNetworkAdapter>>> GetNetAdaptersBySwitch(Guid switchId) =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("All")
            .AddParameter("ErrorAction", "SilentlyContinue")
        from adapters in psEngine.GetObjectsAsync<VMNetworkAdapter>(command).ToAff()
        select adapters.Filter(a => !string.IsNullOrWhiteSpace(a.Value.VMName))
            .Filter(a => a.Value.SwitchId == switchId);

    public Aff<RT, Unit> DisconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters) =>
        from psEngine in default(RT).Powershell
        from _ in adapters
            .Map(adapter => PsCommandBuilder.Create()
                .AddCommand("Disconnect-VMNetworkAdapter")
                .AddParameter("VMNetworkAdapter", adapter.PsObject))
            .Map(cmd => psEngine.RunAsync(cmd).ToAsync().ToAff())
            .SequenceParallel()
        select unit;

    public Aff<RT, Unit> ConnectNetworkAdapters(
        Seq<TypedPsObject<VMNetworkAdapter>> adapters,
        string switchName) =>
        from psEngine in default(RT).Powershell
        from _ in adapters
            .Map(adapter => PsCommandBuilder.Create()
                .AddCommand("Connect-VMNetworkAdapter")
                .AddParameter("VMNetworkAdapter", adapter.PsObject)
                .AddParameter("SwitchName", switchName))
            .Map(cmd => psEngine.RunAsync(cmd).ToAsync().ToAff())
            .SequenceParallel()
        select unit;

    public Aff<RT,Unit> ReconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters) =>
        from psEngine in default(RT).Powershell
        from _ in adapters
            // We resolve the switch by ID so the reconnect succeeds in case
            // there are multiple switches with the same name. Otherwise,
            // the rollback during network configuration might fail.
            .Map(adapter => PsCommandBuilder.Create()
                .AddCommand("Get-VMSwitch")
                .AddParameter("Id", adapter.Value.SwitchId)
                .AddCommand("Connect-VMNetworkAdapter")
                .AddParameter("VMNetworkAdapter", adapter.PsObject))
            .Map(cmd => psEngine.RunAsync(cmd).ToAsync().ToAff())
            .SequenceParallel()
        select unit;

    public Aff<RT, Unit> CreateOverlaySwitch(Seq<string> adapters) =>
        from psEngine in default(RT).Powershell.ToAff()
        let createSwitchCommand = adapters.Count > 0
            ? PsCommandBuilder.Create()
                .AddCommand("New-VMSwitch")
                .AddParameter("Name", EryphConstants.OverlaySwitchName)
                .AddParameter("NetAdapterName", adapters.ToArray())
                .AddParameter("AllowManagementOS", false)
            : PsCommandBuilder.Create()
                .AddCommand("New-VMSwitch")
                .AddParameter("Name", EryphConstants.OverlaySwitchName)
                .AddParameter("SwitchType", "Private")
        from _1 in psEngine.RunAsync(createSwitchCommand).ToAff()
        let enableExtensionCommand = PsCommandBuilder.Create()
            .AddCommand("Enable-VMSwitchExtension")
            .AddParameter("Name", EryphConstants.SwitchExtensionName)
        from _ in psEngine.RunAsync(enableExtensionCommand).ToAff()
        select unit;

    public Aff<RT, Option<OverlaySwitchInfo>> FindOverlaySwitch(
        Seq<VMSwitch> vmSwitches,
        Seq<HostNetworkAdapter> adapters) =>
        from psEngine in default(RT).Powershell
        // Only a single overlay switch exists when the network setup is valid.
        // Otherwise, the network setup needs to be corrected by reapplying the
        // network provider configuration.
        let overlaySwitch = vmSwitches.Find(x => x.Name == EryphConstants.OverlaySwitchName)
        from switchInfo in overlaySwitch
            .Map(s => PrepareOverlaySwitchInfo(s, adapters))
            .Sequence()
        select switchInfo;

    private Aff<RT, OverlaySwitchInfo> PrepareOverlaySwitchInfo(
        VMSwitch overlaySwitch,
        Seq<HostNetworkAdapter> adapters) =>
        from psEngine in default(RT).Powershell
        from switchAdapters in overlaySwitch.NetAdapterInterfaceGuid.ToSeq()
            .Map(guid => adapters.Find(x => x.InterfaceGuid == guid)
                .ToEff(Error.New($"Could not find the host network adapter {guid}")))
            .Sequence()
        let switchAdapterNames = switchAdapters.Map(x => x.Name)
        select new OverlaySwitchInfo(overlaySwitch.Id, toHashSet(switchAdapterNames));

    public Aff<RT,Unit> RemoveOverlaySwitch() =>
        from psEngine in default(RT).Powershell.ToAff()
        let removeSwitchCommand = PsCommandBuilder.Create()
            .AddCommand("Remove-VMSwitch")
            .AddParameter("Name", EryphConstants.OverlaySwitchName)
            .AddParameter("Force")
        from _ in psEngine.RunAsync(removeSwitchCommand).ToAff()
        select unit;

    public Aff<RT,Unit> RemoveNetNat(string natName) =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Remove-NetNat")
            .AddParameter("Name", natName)
            .AddParameter("Confirm", false)
        from _ in psEngine.RunAsync(command).ToAff()
        select unit;

    public Aff<RT, Unit> AddNetNat(string natName, IPNetwork2 network) =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("New-NetNat")
            .AddParameter("Name", natName)
            .AddParameter("InternalIPInterfaceAddressPrefix", network.ToString())
        from _ in psEngine.RunAsync(command).ToAff()
        select unit;

    public Aff<RT,Seq<NetIpAddress>> GetAdapterIpV4Address(string adapterName) =>
        from psEngine in default(RT).Powershell.ToAff()
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-NetIpAddress")
            .AddParameter("InterfaceAlias", adapterName)
            .AddParameter("AddressFamily", "IPv4")
            .AddParameter("ErrorAction", "SilentlyContinue")
        from ipAddresses in psEngine.GetObjectValuesAsync<NetIpAddress>(command).ToAff()
        select ipAddresses;

    public Aff<RT, Unit> WaitForBridgeAdapter(string bridgeName)
    {
        return default(RT).Powershell.Bind(ps =>
        {
            return Prelude.TryAsync(async () =>
                {
                    //make sure adapter is created
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(new TimeSpan(0, 0, 30));
                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(500, cts.Token);
                        var res = await ps.GetObjectsAsync<HostNetworkAdapter>(
                            PsCommandBuilder.Create().AddCommand("Get-NetAdapter")
                                .AddArgument(bridgeName));

                        if (res.IsLeft)
                        {
                            continue;
                        }

                        break;
                    }

                    await Task.Delay(1000, cts.Token); //relax a bit more
                    return Unit.Default;
                }).ToEither(f => Error.New(f.Message))
                .ToAff(f => Error.New($"Could not find adapter for bridge. Error: {f}"));

        });

    }
}