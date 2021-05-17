using System;
using System.Text;
using System.Threading.Tasks;
using Haipa.Resources.Machines.Config;
using Haipa.VmManagement.Data.Full;
using LanguageExt;

namespace Haipa.VmManagement.Converging
{
    public class ConvergeNetworkAdapters : ConvergeTaskBase
    {
        public ConvergeNetworkAdapters(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var adapterConfig = Context.Config.VM.NetworkAdapters;

            var seq = adapterConfig.Map(adapter => NetworkAdapter(adapter, vmInfo)).ToSeq();

            if (seq.IsEmpty)
                return vmInfo;

            return await seq.Last;
        }


        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> NetworkAdapter(
            VirtualMachineNetworkAdapterConfig networkAdapterConfig,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var switchName = string.IsNullOrWhiteSpace(networkAdapterConfig.SwitchName)
                ? "Default Switch"
                : networkAdapterConfig.SwitchName;


            var optionalAdapter = await ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                i => i.NetworkAdapters,
                adapter => networkAdapterConfig.Name.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase),
                async () =>
                {
                    await Context.ReportProgress($"Add Network Adapter: {networkAdapterConfig.Name}")
                        .ConfigureAwait(false);
                    return await Context.Engine.GetObjectsAsync<VMNetworkAdapter>(PsCommandBuilder.Create()
                        .AddCommand("Add-VmNetworkAdapter")
                        .AddParameter("Passthru")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("Name", networkAdapterConfig.Name)
                        .AddParameter("StaticMacAddress", UseOrGenerateMacAddress(networkAdapterConfig, vmInfo))
                        .AddParameter("SwitchName", switchName)).ConfigureAwait(false);
                }).ConfigureAwait(false);


            return await optionalAdapter.BindAsync(async adapter =>
            {
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
    }
}