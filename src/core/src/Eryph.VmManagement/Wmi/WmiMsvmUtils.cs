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

    public static Eff<Option<VirtualMachineState>> getVmState(
        WmiObject managementObject) =>
        from enabledState in getRequiredValue<ushort>(managementObject, "EnabledState")
        let convertedEnabledState = convert<MsvmComputerSystemEnabledState>(enabledState)
        from otherEnabledState in getValue<string>(managementObject, "OtherEnabledState")
        from healthState in getRequiredValue<ushort>(managementObject, "HealthState")
        let convertedHealthState = convert<MsvmComputerSystemHealthState>(healthState)
        let vmState = StateConverter.ConvertVMState(convertedEnabledState, otherEnabledState, convertedHealthState)
        select vmState;

    public static Eff<Option<VirtualMachineOperationalStatus>> getOperationalStatus(
        WmiObject wmiObject) =>
        from operationalStatus in getRequiredValue<ushort[]>(wmiObject, "OperationalStatus")
        let primaryStatus = operationalStatus.Length >= 1
            ? convert<VirtualMachineOperationalStatus>(operationalStatus[0])
            : None
        let secondaryStatus = operationalStatus.Length >= 2
            ? convert<VirtualMachineOperationalStatus>(operationalStatus[1])
            : None
        select OperationalStatusConverter.Convert(primaryStatus, secondaryStatus);

    public static Eff<TimeSpan> GetVmUpTime(
        WmiObject managementObject) =>
        from upTimeMilliseconds in getValue<ulong>(
            managementObject, "OnTimeInMilliseconds")
        from uptime in upTimeMilliseconds
            .Map(t => Eff(() => TimeSpan.FromMilliseconds(t))
                .MapFail(_ => Error.New($"The value '{t}' is not a valid VM uptime.")))
            .Sequence()
        select uptime.IfNone(TimeSpan.Zero);

    private static Option<T> convert<T>(ushort value) where T : struct, Enum =>
        Optional(value)
            .Filter(v => Enum.IsDefined(typeof(T),v))
            .Map(v => (T)Enum.ToObject(typeof(T), v));
}
