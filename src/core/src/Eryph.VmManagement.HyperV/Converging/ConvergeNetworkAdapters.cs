using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.enums;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeNetworkAdapters(ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override Task<Either<Error, Unit>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeAdapters(vmInfo).ToEither();

    private EitherAsync<Error, Unit> ConvergeAdapters(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from adapters in GetVmAdapters(vmInfo)
        from adapterConfigs in PrepareAdapterConfigs().ToAsync()
        from _ in RemoveMissingAdapters(adapters, adapterConfigs)
        from __ in AddOrUpdateAdapters(adapterConfigs, adapters, vmInfo)
        select unit;

    private Either<Error, Seq<PhysicalAdapterConfig>> PrepareAdapterConfigs() =>
        // The adapters are driven by the union of the configured networks and
        // the configured network adapters. Adapters which are not attached to
        // any network are converged as disconnected Hyper-V adapters.
        from adapterNames in (Context.Config.Networks.ToSeq().Map(n => n.AdapterName)
                              + Context.Config.NetworkAdapters.ToSeq().Map(a => a.Name))
            .Map(CatletNetworkAdapterName.NewEither)
            .Sequence()
            .Map(names => names.Distinct())
        from adapterConfigs in adapterNames
            .Map(PrepareAdapterConfig)
            .Sequence()
        select adapterConfigs;

    private Either<Error, PhysicalAdapterConfig> PrepareAdapterConfig(
        CatletNetworkAdapterName adapterName) =>
        Context.NetworkSettings.ToSeq()
            .Find(s => CatletNetworkAdapterName.NewOption(s.AdapterName) == adapterName)
            .Match(
                settings => PrepareConnectedAdapterConfig(adapterName, settings),
                // An adapter is only converged as disconnected when it is not attached
                // to any network. When it is attached to a network but has no network
                // settings, the settings generation is incomplete. We fail instead of
                // silently disconnecting the adapter.
                () => IsAttachedToNetwork(adapterName)
                    ? Left<Error, PhysicalAdapterConfig>(Error.New(
                        $"Could not find the network settings for adapter '{adapterName}' which is attached to a network."))
                    : PrepareDisconnectedAdapterConfig(adapterName));

    private bool IsAttachedToNetwork(CatletNetworkAdapterName adapterName) =>
        Context.Config.Networks.ToSeq()
            .Exists(n => CatletNetworkAdapterName.NewOption(n.AdapterName) == adapterName);

    private Either<Error, PhysicalAdapterConfig> PrepareConnectedAdapterConfig(
        CatletNetworkAdapterName adapterName,
        MachineNetworkSettings settings) =>
        from switchName in Context.HostInfo.FindSwitchName(settings.NetworkProviderName ?? "")
            .ToEither(Error.New($"Could not find network provider '{settings.NetworkProviderName}' on host."))
        select new PhysicalAdapterConfig(
            adapterName.Value,
            switchName,
            settings.MacAddress ?? "",
            settings.PortName,
            settings.NetworkName,
            settings.MacAddressSpoofing,
            settings.DhcpGuard,
            settings.RouterGuard);

    // An adapter which is not attached to any network is converged as a
    // disconnected Hyper-V adapter. It has no virtual switch and no OVS port.
    // The MAC address is taken from the catlet config (it is generated during
    // instantiation and persisted as part of the inventoried catlet config).
    private Either<Error, PhysicalAdapterConfig> PrepareDisconnectedAdapterConfig(
        CatletNetworkAdapterName adapterName) =>
        from adapterConfig in Context.Config.NetworkAdapters.ToSeq()
            .Find(a => CatletNetworkAdapterName.NewOption(a.Name) == adapterName)
            .ToEither(Error.New($"Could not find the configuration for adapter '{adapterName}'."))
        // The MAC address is assigned when the catlet config is instantiated. It
        // should always be present here. We guard explicitly so a missing MAC does
        // not surface as a confusing 'invalid MAC address' error.
        from rawMacAddress in Optional(adapterConfig.MacAddress).Filter(notEmpty)
            .ToEither(() => Error.New(
                $"The adapter '{adapterName}' is not attached to any network and has no MAC address. "
                + "The catlet configuration might not have been instantiated correctly."))
        from macAddress in EryphMacAddress.NewEither(rawMacAddress)
            .MapLeft(e => Error.New(
                $"The MAC address of the adapter '{adapterName}' which is not attached to any network is invalid.", e))
        select new PhysicalAdapterConfig(
            adapterName.Value,
            None,
            macAddress.Value,
            None,
            None,
            None,
            None,
            None);

    private EitherAsync<Error, Seq<TypedPsObject<VMNetworkAdapter>>> GetVmAdapters(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
        from adapters in Context.Engine.GetObjectsAsync<VMNetworkAdapter>(command)
        select adapters;

    private EitherAsync<Error, Unit> RemoveMissingAdapters(
        Seq<TypedPsObject<VMNetworkAdapter>> adapters,
        Seq<PhysicalAdapterConfig> adapterConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let configuredAdapterNames = toHashSet(adapterConfig
            .Map(c => CatletNetworkAdapterName.New(c.AdapterName)))
        from __ in adapters
            .Filter(a => CatletNetworkAdapterName.NewOption(a.Value.Name)
                .Map(n => !configuredAdapterNames.Contains(n))
                .IfNone(true))
            .Map(RemoveAdapter)
            .SequenceSerial()
        select unit;

    private EitherAsync<Error, Unit> RemoveAdapter(
        TypedPsObject<VMNetworkAdapter> adapter) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Remove-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
        from __ in Context.Engine.RunAsync(command)
        select unit;

    private EitherAsync<Error, Unit> AddOrUpdateAdapters(
        Seq<PhysicalAdapterConfig> adapterConfigs,
        Seq<TypedPsObject<VMNetworkAdapter>> adapters,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in adapterConfigs
            .Map(c => AddOrUpdateAdapter(c, adapters, vmInfo))
            .SequenceSerial()
        select unit;

    private EitherAsync<Error, Unit> AddOrUpdateAdapter(
        PhysicalAdapterConfig adapterConfig,
        Seq<TypedPsObject<VMNetworkAdapter>> adapters,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let adapterName = CatletNetworkAdapterName.New(adapterConfig.AdapterName)
        let adapter = adapters.Find(a => CatletNetworkAdapterName.NewOption(a.Value.Name) == adapterName)
        from __ in adapter.Match(
            a => UpdateAdapter(adapterConfig, a),
            () => AddAdapter(adapterConfig, vmInfo))
        select unit;

    private EitherAsync<Error, Unit> AddAdapter(
        PhysicalAdapterConfig adapterConfig,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _1 in Context.ReportProgressAsync($"Add Network Adapter: {adapterConfig.AdapterName}")
        let baseCommand = PsCommandBuilder.Create()
            .AddCommand("Add-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Name", adapterConfig.AdapterName)
            .AddParameter("StaticMacAddress", adapterConfig.MacAddress)
        // When the adapter is not attached to any network, it is created as a
        // disconnected adapter, i.e. without a virtual switch.
        let command = adapterConfig.SwitchName
            .Map(switchName => baseCommand.AddParameter("SwitchName", switchName))
            .IfNone(baseCommand)
            .AddParameter("Passthru")
        from optionalCreatedAdapter in Context.Engine.GetObjectAsync<VMNetworkAdapter>(command)
        from createdAdapter in optionalCreatedAdapter.ToEitherAsync(
            Error.New("Failed to create network adapter"))
        // A disconnected adapter has no OVS port.
        from _2 in adapterConfig.PortName.Match(
            // Sometimes, it takes a moment until we can actually read the OVS port name
            // from a newly created adapter. In the end, we need to access the
            // Msvm_EthernetPortAllocationSettingData for that adapter via WMI.
            portName =>
                from __ in WaitForPortName(createdAdapter.Value.Id ?? throw new InvalidOperationException("The created network adapter has no ID.")).Run().ToEitherAsync()
                from ___ in Context.PortManager.SetPortName(createdAdapter.Value.Id ?? throw new InvalidOperationException("The created network adapter has no ID."), portName)
                select unit,
            () => RightAsync<Error, Unit>(unit))
        from _3 in ConvergeSecuritySettings(createdAdapter, adapterConfig)
        select unit;

    private EitherAsync<Error, Unit> UpdateAdapter(
        PhysicalAdapterConfig adapterConfig,
        TypedPsObject<VMNetworkAdapter> adapter) =>
        // A disconnected adapter has no OVS port. Any stale OVS port name (WMI
        // metadata) is left in place: it is deterministic per adapter
        // (ovs_<catletId>_<adapter>), so the same name is reapplied when a network
        // is attached again. While disconnected the adapter is on no switch, hence
        // the leftover name is inert.
        from _1 in adapterConfig.PortName.Match(
            portName =>
                from currentPortName in Context.PortManager.GetPortName(adapter.Value.Id ?? throw new InvalidOperationException("The network adapter has no ID."))
                from __ in currentPortName == portName
                    ? RightAsync<Error, Unit>(unit)
                    : Context.PortManager.SetPortName(adapter.Value.Id, portName)
                select unit,
            () => RightAsync<Error, Unit>(unit))
        from _2 in adapter.Value.MacAddress == adapterConfig.MacAddress
            ? RightAsync<Error, Unit>(unit)
            : UpdateMacAddress(adapter, adapterConfig.MacAddress)
        from _3 in ConvergeSecuritySettings(adapter, adapterConfig)
        from _4 in ConvergeConnection(adapter, adapterConfig)
        select unit;

    private EitherAsync<Error, Unit> ConvergeConnection(
        TypedPsObject<VMNetworkAdapter> adapter,
        PhysicalAdapterConfig adapterConfig) =>
        adapterConfig.SwitchName.Match(
            switchName =>
                adapter.Value.Connected && adapter.Value.SwitchName == switchName
                    ? RightAsync<Error, Unit>(unit)
                    : ConnectAdapter(adapter, switchName, adapterConfig.NetworkName),
            // The adapter is not attached to any network. Disconnect it from
            // its virtual switch if necessary.
            () => adapter.Value.Connected
                ? DisconnectAdapter(adapter)
                : RightAsync<Error, Unit>(unit));

    private EitherAsync<Error, Unit> UpdateMacAddress(
        TypedPsObject<VMNetworkAdapter> adapter,
        string macAddress) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
            .AddParameter("StaticMacAddress", macAddress)
        from __ in Context.Engine.RunAsync(command)
        select unit;

    private EitherAsync<Error, Unit> ConvergeSecuritySettings(
        TypedPsObject<VMNetworkAdapter> adapter,
        PhysicalAdapterConfig adapterConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let currentMacAddressSpoofing = adapter.Value.MacAddressSpoofing == OnOffState.On
        let currentRouterGuard = adapter.Value.RouterGuard == OnOffState.On
        let currentDhcpGuard = adapter.Value.DhcpGuard == OnOffState.On
        let changedMacAddressSpoofing = adapterConfig.MacAddressSpoofing
            .Filter(v => v != currentMacAddressSpoofing)
        let changedDhcpGuard = adapterConfig.DhcpGuard
            .Filter(v => v != currentDhcpGuard)
        let changedRouterGuard = adapterConfig.RouterGuard
            .Filter(v => v != currentRouterGuard)
        from __ in changedMacAddressSpoofing.IsSome || changedDhcpGuard.IsSome || changedRouterGuard.IsSome
            ? UpdateSecuritySettings(
                adapter,
                changedMacAddressSpoofing,
                changedDhcpGuard,
                changedRouterGuard)
            : RightAsync<Error, Unit>(unit)
        select unit;

    private EitherAsync<Error, Unit> UpdateSecuritySettings(
        TypedPsObject<VMNetworkAdapter> adapter,
        Option<bool> macAddressSpoofing,
        Option<bool> dhcpGuard,
        Option<bool> routerGuard) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
        let command2 = macAddressSpoofing
            .Map(v => v ? OnOffState.On : OnOffState.Off)
            .Map(s => command.AddParameter("MacAddressSpoofing", s))
            .IfNone(command)
        let command3 = dhcpGuard
            .Map(v => v ? OnOffState.On : OnOffState.Off)
            .Map(s => command2.AddParameter("DhcpGuard", s))
            .IfNone(command2)
        let command4 = routerGuard
            .Map(v => v ? OnOffState.On : OnOffState.Off)
            .Map(s => command3.AddParameter("RouterGuard", s))
            .IfNone(command3)
        from __ in Context.Engine.RunAsync(command4)
        select unit;

    private EitherAsync<Error, Unit> ConnectAdapter(
        TypedPsObject<VMNetworkAdapter> adapter,
        string switchName,
        Option<string> networkName) =>
        from _ in Context.ReportProgressAsync(
            $"Connected Network Adapter {adapter.Value.Name} to network {networkName.IfNone("")}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Connect-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
            .AddParameter("SwitchName", switchName)
        from __ in Context.Engine.RunAsync(command)
        select unit;

    private EitherAsync<Error, Unit> DisconnectAdapter(
        TypedPsObject<VMNetworkAdapter> adapter) =>
        from _ in Context.ReportProgressAsync(
            $"Disconnected Network Adapter {adapter.Value.Name}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Disconnect-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
        from __ in Context.Engine.RunAsync(command)
        select unit;

    private Aff<Unit> WaitForPortName(string adapterId) =>
        from portNameExists in repeatUntil(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(1))
            & Schedule.upto(TimeSpan.FromSeconds(10)),
            from portName in Context.PortManager.GetPortName(adapterId).ToAff(e => e)
            select portName.IsSome,
            r => r)
        from _ in guard(portNameExists,
            Error.New($"The Hyper-V network adapter '{adapterId}' has not been successfully created."))
        select unit;

    private sealed record PhysicalAdapterConfig(
        string AdapterName,
        // The switch, port name and network name are not set for adapters
        // which are not attached to any network (disconnected adapters).
        Option<string> SwitchName,
        string MacAddress,
        Option<string> PortName,
        Option<string> NetworkName,
        // The security settings are only managed for adapters which are
        // attached to a network. For disconnected adapters they are None.
        Option<bool> MacAddressSpoofing,
        Option<bool> DhcpGuard,
        Option<bool> RouterGuard);
}
