using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeNetworkAdapters : ConvergeTaskBase
    {


        public ConvergeNetworkAdapters(ConvergeContext context) : base(context)
        {
        }

        public override Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var interfaceCounter = 0;
            var adapters = Context.Config.VM.NetworkAdapters.ToArr();

            return Context.Config.Networks
                .Map<MachineNetworkConfig, Either<PowershellFailure, PhysicalAdapterConfig>>
                (n =>
                {
                    var networkName = n.Name ?? "default";

                    if (networkName == "default")
                        networkName = Context.HostSettings.DefaultNetwork;

                    var hostNetwork = Context.HostInfo.VirtualNetworks.FirstOrDefault(x => x.Name == networkName);
                    if (hostNetwork == null)
                        return Prelude.Left(new PowershellFailure
                            { Message = $"Could not find network '{n.Name}' on Host." });

                    var switchConfig =
                        Context.HostInfo.Switches.First(x => x.Id == hostNetwork.VirtualSwitchId.ToString());

                    return Prelude.Right(new PhysicalAdapterConfig(n.AdapterName ?? "eth" + interfaceCounter++,
                        switchConfig.VirtualSwitchName, null));

                })
                .MapT(c =>
                {
                    return adapters.Find(x => x.Name == c.AdapterName).Match(a =>
                        {
                            adapters = adapters.Remove(a);
                            return c.Apply(macAddress: UseOrGenerateMacAddress(a, vmInfo));
                        },
                        () => c.Apply(macAddress: GenerateMacAddress(vmInfo.Value.Id, c.AdapterName)));

                })
                .Map(e => e.ToAsync())
                .BindT(c => NetworkAdapter(c, vmInfo).ToAsync())
                .TraverseSerial(l => l)
                .Map(e => vmInfo.Recreate())
                .ToEither();
                

        }


        private async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> NetworkAdapter(
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
                if (adapter.Value.Connected && adapter.Value.SwitchName == switchName)
                    return Unit.Default;

                await Context.ReportProgress(
                        $"Connecting Network Adapter {adapter.Value.Name} to switch {switchName}")
                    .ConfigureAwait(false);
                return await Context.Engine.RunAsync(
                    PsCommandBuilder.Create().AddCommand("Connect-VmNetworkAdapter")
                        .AddParameter("VMNetworkAdapter", adapter.PsObject)
                        .AddParameter("SwitchName", switchName)).ConfigureAwait(false);
            }).BindAsync(_ => vmInfo.RecreateOrReload(Context.Engine)).ConfigureAwait(false);
        }

        private static string UseOrGenerateMacAddress(VirtualMachineNetworkAdapterConfig adapterConfig,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var result = adapterConfig.MacAddress;
            if (string.IsNullOrWhiteSpace(result))
                result = GenerateMacAddress(vmInfo.Value.Id, adapterConfig.Name);
            return result;
        }

        private static string GenerateMacAddress(Guid valueId, string adapterName)
        {
            var id = $"{valueId}_{adapterName}";
            var crc = new Crc32();

            string result = null;

            var arrayData = Encoding.ASCII.GetBytes(id);
            var arrayResult = crc.ComputeHash(arrayData);
            foreach (var t in arrayResult)
            {
                var temp = Convert.ToString(t, 16);
                if (temp.Length == 1)
                    temp = $"0{temp}";
                result += temp;
            }

            return "d2ab" + result;
        }


        private class PhysicalAdapterConfig
        {
            public readonly string AdapterName;
            public readonly string SwitchName;
            public readonly string MacAddress;

            public PhysicalAdapterConfig(string adapterName, string switchName, string macAddress)
            {
                AdapterName = adapterName;
                SwitchName = switchName;
                MacAddress = macAddress;
            }

            public PhysicalAdapterConfig Apply(string adapterName = null, string switchName=null, string macAddress = null)
            {
                return new PhysicalAdapterConfig(
                    adapterName ?? AdapterName, 
                    switchName ?? SwitchName,
                    macAddress ?? MacAddress);
            }
        }
    }

}