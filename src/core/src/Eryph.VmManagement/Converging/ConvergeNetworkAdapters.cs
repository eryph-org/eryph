using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Data;
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
        from adapterConfigs in Context.Config.Networks.ToSeq()
            .Map(PrepareAdapterConfig)
            .Sequence()
        select adapterConfigs;

    private Either<Error, PhysicalAdapterConfig> PrepareAdapterConfig(
        CatletNetworkConfig networkConfig) =>
        from adapterName in CatletNetworkAdapterName.NewEither(networkConfig.AdapterName)
        from settings in Context.NetworkSettings.ToSeq()
            .Find(s => CatletNetworkAdapterName.NewOption(s.AdapterName) == adapterName)
            .ToEither(Error.New($"Could not find network settings for adapter {networkConfig.AdapterName}"))
        from switchName in Context.HostInfo.FindSwitchName(settings.NetworkProviderName)
            .ToEither(Error.New($"Could not find network provider '{settings.NetworkProviderName}' on host."))
        select new PhysicalAdapterConfig(
            adapterName.Value,
            switchName,
            settings.MacAddress,
            settings.PortName,
            settings.NetworkName,
            settings.MacAddressSpoofing,
            settings.DhcpGuard,
            settings.RouterGuard);

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
            Some: a => UpdateAdapter(adapterConfig, a),
            None: () => AddAdapter(adapterConfig, vmInfo))
        select unit;

    private EitherAsync<Error, Unit> AddAdapter(
        PhysicalAdapterConfig adapterConfig,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _1 in Context.ReportProgressAsync($"Add Network Adapter: {adapterConfig.AdapterName}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Add-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Name", adapterConfig.AdapterName)
            .AddParameter("StaticMacAddress", adapterConfig.MacAddress)
            .AddParameter("SwitchName", adapterConfig.SwitchName)
            .AddParameter("Passthru")
        from optionalCreatedAdapter in Context.Engine.GetObjectAsync<VMNetworkAdapter>(command)
        from createdAdapter in optionalCreatedAdapter.ToEitherAsync(
            Error.New("Failed to create network adapter"))
        // Sometimes, it takes a moment until we can actually read the OVS port name
        // from a newly created adapter. In the end, we need to access the
        // Msvm_EthernetPortAllocationSettingData for that adapter via WMI.
        from _2 in WaitForPortName(createdAdapter.Value.Id).Run().ToEitherAsync()
        from _3 in Context.PortManager.SetPortName(createdAdapter.Value.Id, adapterConfig.PortName)
        from _4 in ConvergeSecuritySettings(createdAdapter, adapterConfig)
        select unit;

    private EitherAsync<Error, Unit> UpdateAdapter(
        PhysicalAdapterConfig adapterConfig,
        TypedPsObject<VMNetworkAdapter> adapter) =>
        from currentPortName in Context.PortManager.GetPortName(adapter.Value.Id)
        from _1 in currentPortName == adapterConfig.PortName
            ? RightAsync<Error, Unit>(unit)
            : Context.PortManager.SetPortName(adapter.Value.Id, adapterConfig.PortName)
        from _2 in adapter.Value.MacAddress == adapterConfig.MacAddress
            ? RightAsync<Error, Unit>(unit)
            : UpdateMacAddress(adapter, adapterConfig.MacAddress)
        from _3 in ConvergeSecuritySettings(adapter, adapterConfig)
        from _4 in adapter.Value.Connected && adapter.Value.SwitchName == adapterConfig.SwitchName
            ? RightAsync<Error, Unit>(unit)
            : ConnectAdapter(adapter, adapterConfig)
        select unit;

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
        let changedMacAddressSpoofing = Optional(adapterConfig.MacAddressSpoofing)
            .Filter(v => v != currentMacAddressSpoofing)
        let changedDhcpGuard = Optional(adapterConfig.DhcpGuard)
            .Filter(v => v != currentDhcpGuard)
        let changedRouterGuard = Optional(adapterConfig.RouterGuard)
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
        PhysicalAdapterConfig adapterConfig) =>
        from _ in Context.ReportProgressAsync(
            $"Connected Network Adapter {adapter.Value.Name} to network {adapterConfig.NetworkName}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Connect-VMNetworkAdapter")
            .AddParameter("VMNetworkAdapter", adapter.PsObject)
            .AddParameter("SwitchName", adapterConfig.SwitchName)
        from __ in Context.Engine.RunAsync(command)
        select unit;

    private Aff<Unit> WaitForPortName(string adapterId) =>
        from portNameExists in repeatUntil(
            Schedule.NoDelayOnFirst
            & Schedule.spaced(TimeSpan.FromSeconds(1))
            & Schedule.upto(TimeSpan.FromSeconds(10)),
            from portName in Context.PortManager.GetPortNameSafe(adapterId).ToAff(e => e)
            select portName.IsSome,
            r => r)
        from _ in guard(portNameExists,
            Error.New($"The Hyper-V network adapter '{adapterId}' has not been successfully created."))
        select unit;

    private sealed record PhysicalAdapterConfig(
        string AdapterName,
        string SwitchName,
        string MacAddress,
        string PortName,
        string NetworkName,
        bool MacAddressSpoofing,
        bool DhcpGuard,
        bool RouterGuard);
}
