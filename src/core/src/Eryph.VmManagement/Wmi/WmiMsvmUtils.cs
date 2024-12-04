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
    /// <summary>
    /// Returns the ID of the VM which is referenced by the given <paramref name="wmiObject"/>.
    /// The method returns <see cref="None"/> when the <paramref name="wmiObject"/>
    /// represents an instance of <c>Msvm_ComputerSystem</c> which is the host.
    /// </summary>
    /// <remarks>
    /// The WMI class <c>Msvm_ComputerSystem</c> can represent both the host or a VM.
    /// The fields <c>Caption</c> and <c>Description</c> both contain information which
    /// indicates whether the instance is the host or a VM. Unfortunately, both fields
    /// are localized and will contain different values depending on the system language.
    /// Hence, the best solution is just trying to extract the VM ID.
    /// </remarks>
    public static Eff<Option<Guid>> GetVmId(
        WmiObject wmiObject) =>
        from className in getRequiredValue<string>(wmiObject, "__CLASS")
        from vmId in className switch
        {
            "Msvm_ComputerSystem" =>
                from name in getRequiredValue<string>(wmiObject, "Name")
                // Msvm_ComputerSystem can be either the host or a VM. For VMs, the name
                // contains the Guid which identifies the VM.
                let vmId = parseGuid(name)
                select vmId,
            "Msvm_GuestNetworkAdapterConfiguration" =>
                from instanceId in getRequiredValue<string>(wmiObject, "InstanceID")
                let parts = instanceId.Split('\\')
                from _ in guard(parts.Length == 3,
                    Error.New($"The instance ID '{instanceId}' is malformed."))
                from vmId in parseGuid(parts[1])
                    .ToEff($"The instance ID '{instanceId} does not contain a valid VM ID.")
                select Some(vmId),
            _ => FailEff<Option<Guid>>(
                Error.New($"WMI objects of type '{className}' are not supported."))
        }
        select vmId;

    public static Eff<VirtualMachineState> GetVmState(
        WmiObject managementObject) =>
        from enabledState in getRequiredValue<ushort>(managementObject, "EnabledState")
        from otherEnabledState in getValue<string>(managementObject, "OtherEnabledState")
        from healthState in getRequiredValue<ushort>(managementObject, "HealthState")
        let vmState = StateConverter.ConvertVMState(enabledState, otherEnabledState, healthState)
        select vmState;

    public static Eff<TimeSpan> GetVmUpTime(
        WmiObject managementObject) =>
        from upTimeMilliseconds in getValue<ulong>(
            managementObject, "OnTimeInMilliseconds")
        from uptime in upTimeMilliseconds
            .Map(t => Eff(() => TimeSpan.FromMilliseconds(t))
                .MapFail(_ => Error.New($"The value '{t}' is not a valid VM uptime.")))
            .Sequence()
        select uptime.IfNone(TimeSpan.Zero);
}
