using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
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
}
