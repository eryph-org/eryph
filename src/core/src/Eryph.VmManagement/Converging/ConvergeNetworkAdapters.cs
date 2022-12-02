using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Machine;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeNetworkAdapters : ConvergeTaskBase
    {


        public ConvergeNetworkAdapters(ConvergeContext context) : base(context)
        {
        }

        public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var interfaceCounter = 0;
            var adapters = Context.Config.VM.NetworkAdapters.ToArr();

            return Context.Config.Networks
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
                                return Prelude.Left<Error,PhysicalAdapterConfig>(Error.New(
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

                var res = await Context.Engine.RunAsync(
                    PsCommandBuilder.Create()
                        .Script(OVSPortScript)).ToError().ConfigureAwait(false); ;

                if (res.IsLeft)
                    return res;

                res = await Context.Engine.RunAsync(
                    PsCommandBuilder.Create()
                        .AddCommand("Set-VMNetworkAdapterOVSPort")
                        .AddParameter("VMNetworkAdapter", adapter.PsObject)
                        .AddParameter("OVSPortName", networkAdapterConfig.PortName)).ToError().ConfigureAwait(false);

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




        private class PhysicalAdapterConfig
        {
            public readonly string AdapterName;
            public readonly string SwitchName;
            public readonly string MacAddress;
            public readonly string PortName;
            public readonly string NetworkName;

            public PhysicalAdapterConfig(string adapterName, string switchName, string macAddress, string portName, string networkName)
            {
                AdapterName = adapterName;
                SwitchName = switchName;
                MacAddress = macAddress;
                PortName = portName;
                NetworkName = networkName;
            }

            public PhysicalAdapterConfig Apply(
                string adapterName = null, string switchName=null, string macAddress = null, string portName = null, string networkName = null)
            {
                return new PhysicalAdapterConfig(
                    adapterName ?? AdapterName, 
                    switchName ?? SwitchName,
                    macAddress ?? MacAddress, 
                    portName ?? PortName,
                    networkName ?? NetworkName);
            }
        }
 


    private const string OVSPortScript = @"

            $WMI_JOB_STATUS_STARTED = 4096
            $WMI_JOB_STATE_RUNNING = 4
            $WMI_JOB_STATE_COMPLETED = 7


            function CheckWMIReturnValue($retVal)
            {
                if ($retVal.ReturnValue -ne 0)
                {
                    if ($retVal.ReturnValue -eq $WMI_JOB_STATUS_STARTED)
                    {
                        do
                        {
                            $job = [wmi]$retVal.Job
                        }
                        while ($job.JobState -eq $WMI_JOB_STATE_RUNNING)

                        if ($job.JobState -ne $WMI_JOB_STATE_COMPLETED)
                        {
                            echo $job.ReturnValue
                            $errorString = ""Job Failed. Job State: "" + $job.JobState.ToString()
                            if ($job.__CLASS -eq ""Msvm_ConcreteJob"")
                            {
                                $errorString += "" Error Code: "" + $job.ErrorCode.ToString()
                                $errorString += "" Error Details: "" + $job.ErrorDescription
                            }
                            else
                            {
                                $error = $job.GetError()
                                if ($error.Error)
                                {
                                    $errorString += "" Error:"" + $error.Error
                                }
                            }
                            throw $errorString
                        }
                    }
                    else
                    {
                        throw ""Job Failed. Return Value: {0}"" -f $job.ReturnValue
                    }
                }
            }


            function Set-VMNetworkAdapterOVSPort
            {
                [CmdletBinding()]
                param
                (
                    [parameter(Mandatory=$true, ValueFromPipeline=$true)]
                    [Microsoft.HyperV.PowerShell.VMNetworkAdapter]$VMNetworkAdapter,

                    [parameter(Mandatory=$true)]
                    [ValidateLength(1, 48)]
                    [string]$OVSPortName
                )
                process
                {
                    $ns = ""root\virtualization\v2""
                    $EscapedId = $VMNetworkAdapter.Id.Replace('\', '\\')

                    $sd = Get-CimInstance -namespace $ns -class Msvm_EthernetPortAllocationSettingData -Filter ""ElementName = '$OVSPortName'""
                    if($sd)
                    {
                        if($sd.InstanceId.Contains($VMNetworkAdapter.Id))
                        {
                            return;
                        }
                        throw ""Cannot assign the overlay port name '$OVSPortName' as it is already assigned to an other port.""
                    }

                    $sd = Get-CimInstance -namespace $ns -class Msvm_EthernetPortAllocationSettingData -Filter ""InstanceId like '$EscapedId%'""

                    if($sd)
                    {
                        $sd.ElementName = $OVSPortName

                        $cimSerializer = [Microsoft.Management.Infrastructure.Serialization.CimSerializer]::Create()
                        $serializedInstance = $cimSerializer.Serialize($sd, [Microsoft.Management.Infrastructure.Serialization.InstanceSerializationOptions]::None)
                        $embeddedInstanceString = [System.Text.Encoding]::Unicode.GetString($serializedInstance)

                        $vsms = Get-CimInstance -namespace $ns -class Msvm_VirtualSystemManagementService
                        $retVal = Invoke-CimMethod -CimInstance $vsms -MethodName ModifyResourceSettings @{ResourceSettings = @($embeddedInstanceString)}

                        try
                        {
                            CheckWMIReturnValue $retVal
                        }
                        catch
                        {
                            throw ""Assigning overlay port name '$OVSPortName' failed""
                        }
                    }
                }
            }

            ";
    }
}