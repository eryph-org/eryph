using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeNetworkAdapters(ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeAdapters(vmInfo).ToEither();

    private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeAdapters(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from adapters in GetVmAdapters(vmInfo)
        from adapterConfigs in PrepareAdapterConfigs().ToAsync()
        from _ in RemoveMissingAdapters(adapters, adapterConfigs)
        from __ in AddOrUpdateAdapters(adapterConfigs, adapters, vmInfo)
        from updatedVmInfo in vmInfo.Reload(Context.Engine)
        select updatedVmInfo;

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
            settings.NetworkName);

    private EitherAsync<Error, Seq<TypedPsObject<VMNetworkAdapter>>> GetVmAdapters(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
        from adapters in Context.Engine.GetObjectsAsync<VMNetworkAdapter>(command)
            .ToError()
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
        from __ in Context.Engine.RunAsync(command).ToError().ToAsync()
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
        from _ in Context.ReportProgressAsync($"Add Network Adapter: {adapterConfig.AdapterName}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Add-VMNetworkAdapter")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Name", adapterConfig.AdapterName)
            .AddParameter("StaticMacAddress", adapterConfig.MacAddress)
            .AddParameter("SwitchName", adapterConfig.SwitchName)
            .AddParameter("Passthru")
        from createdAdapters in Context.Engine.GetObjectValuesAsync<VMNetworkAdapter>(command)
            .ToError()
        from createdAdapter in createdAdapters.HeadOrNone()
            .ToEitherAsync(Error.New("Failed to create network adapter"))
        from __ in Context.PortManager.SetPortName(createdAdapter.Id, adapterConfig.PortName)
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
        from _3 in adapter.Value.Connected && adapter.Value.SwitchName == adapterConfig.SwitchName
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
        from __ in Context.Engine.RunAsync(command).ToError().ToAsync()
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
        from __ in Context.Engine.RunAsync(command).ToError().ToAsync()
        select unit;

    private sealed record PhysicalAdapterConfig(
        string AdapterName,
        string SwitchName,
        string MacAddress,
        string PortName,
        string NetworkName);
}
