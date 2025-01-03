﻿using System;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeNetworkAdapters(ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var interfaceCounter = 0;

        return await Context.Config.Networks
            .Map
            (n =>
            {
                return Context.NetworkSettings.Find(x => x.AdapterName == n.AdapterName)
                    .ToEither(Error.New($"Could not find network settings for adapter {n.AdapterName}"))
                    .Bind(ms =>
                    {

                        var switchName =
                            Context.HostInfo.FindSwitchName(ms.NetworkProviderName);

                        if (switchName == null)
                            return Left<Error,PhysicalAdapterConfig>(Error.New(
                                $"Could not find network provider '{ms.NetworkProviderName}' on Host."));

                        return new PhysicalAdapterConfig(n.AdapterName ?? "eth" + interfaceCounter++,
                            switchName, ms.MacAddress, ms.PortName, ms.NetworkName);
                    });

            })
            .Map(e => e.ToAsync())
            .BindT(c => NetworkAdapter(c, vmInfo).ToAsync())
            .TraverseSerial(l => l)
            .Map(e => vmInfo.Recreate())
            .ToEither();
    }

    private async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> NetworkAdapter(
        PhysicalAdapterConfig networkAdapterConfig,
        TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var switchName = string.IsNullOrWhiteSpace(networkAdapterConfig.SwitchName)
            ? "Default Switch"
            : networkAdapterConfig.SwitchName;


        var optionalAdapter = await ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
            i => i.NetworkAdapters,
            device => device.Cast<VMNetworkAdapter>()
                .Map(adapter => 
                    networkAdapterConfig.AdapterName.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase)),
            async () =>
            {
                await Context.ReportProgress($"Add Network Adapter: {networkAdapterConfig.AdapterName}")
                    .ConfigureAwait(false);
                return await Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(PsCommandBuilder.Create()
                    .AddCommand("Add-VmNetworkAdapter")
                    .AddParameter("Passthru")
                    .AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Name", networkAdapterConfig.AdapterName)
                    .AddParameter("StaticMacAddress", networkAdapterConfig.MacAddress)
                    .AddParameter("SwitchName", switchName)).ConfigureAwait(false);
            }).ConfigureAwait(false);


        return await optionalAdapter.BindAsync(async device =>
        {
            var adapter = device.Cast<VMNetworkAdapter>();

            var res = await Context.PortManager.SetPortName(adapter.Value.Id, networkAdapterConfig.PortName);
            if (res.IsLeft)
                return res;

            if (adapter.Value.MacAddress != networkAdapterConfig.MacAddress)
            {
                res = await Context.Engine.RunAsync(
                    PsCommandBuilder.Create().AddCommand("Set-VmNetworkAdapter")
                        .AddParameter("VMNetworkAdapter", adapter.PsObject)
                        .AddParameter("StaticMacAddress", networkAdapterConfig.MacAddress)).ToError().ConfigureAwait(false);

                if (res.IsLeft)
                    return res;

            }
                
            if (adapter.Value.Connected && adapter.Value.SwitchName == switchName)
                return Unit.Default;

            await Context.ReportProgress(
                    $"Connected Network Adapter {adapter.Value.Name} to network {networkAdapterConfig.NetworkName}")
                .ConfigureAwait(false);
            return await Context.Engine.RunAsync(
                PsCommandBuilder.Create().AddCommand("Connect-VmNetworkAdapter")
                    .AddParameter("VMNetworkAdapter", adapter.PsObject)
                    .AddParameter("SwitchName", switchName)).ToError().ConfigureAwait(false);
        }).BindAsync(_ => vmInfo.RecreateOrReload(Context.Engine).ToEither()).ConfigureAwait(false);
    }

    private sealed record PhysicalAdapterConfig(
        string AdapterName,
        string SwitchName,
        string MacAddress,
        string PortName,
        string NetworkName);
}
