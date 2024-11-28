using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static Eryph.VmManagement.Wmi.WmiUtils;

namespace Eryph.VmManagement.Wmi;

public static class WmiMsvmUtils
{
    public static Eff<Option<Guid>> GetVmId(
        ManagementBaseObject managementObject) =>
        from vmId in managementObject.ClassPath.ClassName switch
        {
            "Msvm_ComputerSystem" =>
                from name in GetPropertyValue<string>(managementObject, "Name")
                // Msvm_ComputerSystem can be either the host or a VM. For VMs, the name
                // contains the Guid which identifies the VM.
                let vmId = parseGuid(name)
                select vmId,
            "Msvm_GuestNetworkAdapterConfiguration" =>
                from instanceId in GetPropertyValue<string>(managementObject, "InstanceID")
                let parts = instanceId.Split('\\')
                from _ in guard(parts.Length == 3,
                    Error.New($"The instance ID '{instanceId}' is malformed."))
                from vmId in parseGuid(parts[1])
                    .ToEff($"The instance ID '{instanceId} does not contain a valid VM ID.")
                select Some(vmId),
            _ => FailEff<Option<Guid>>(
                Error.New($"WMI objects of type {managementObject.ClassPath.ClassName} are not supported."))
        }
        select vmId;

    public static Eff<VirtualMachineState> GetVmState(
        ManagementBaseObject managementObject) =>
        from enabledState in GetPropertyValue<ushort>(managementObject, "EnabledState")
        from otherEnabledState in GetOptionalPropertyValue<string>(managementObject, "OtherEnabledState")
        from healthState in GetPropertyValue<ushort>(managementObject, "HealthState")
        let vmState = StateConverter.ConvertVMState(enabledState, otherEnabledState, healthState)
        select vmState;

    public static Eff<TimeSpan> GetVmUpTime(
        ManagementBaseObject managementObject) =>
        from upTimeMilliseconds in GetOptionalPropertyValue<ulong>(
            managementObject, "OnTimeInMilliseconds")
        from uptime in upTimeMilliseconds
            .Map(t => Eff(() => TimeSpan.FromMilliseconds(t))
                .MapFail(_ => Error.New($"The value '{t}' is not a valid VM uptime.")))
            .Sequence()
        select uptime.IfNone(TimeSpan.Zero);
}
