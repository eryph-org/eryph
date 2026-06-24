using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.enums;

namespace Eryph.VmManagement.Data.unused;

public class VirtualMachineIntegrationComponentInfo : VirtualMachineDeviceInfo
{
    public bool Enabled { get; set; }

    public VMIntegrationComponentOperationalStatus[] OperationalStatus { get; set; }

    public VMIntegrationComponentOperationalStatus? PrimaryOperationalStatus
    {
        get
        {
            var operationalStatus = OperationalStatus;
            return operationalStatus.Length != 0
                ? operationalStatus[0]
                : null;
        }
    }

    public string PrimaryStatusDescription
    {
        get
        {
            var statusDescription = StatusDescription;
            return statusDescription.Length != 0 ? statusDescription[0] : null;
        }
    }

    public VMIntegrationComponentOperationalStatus? SecondaryOperationalStatus
    {
        get
        {
            var operationalStatus = OperationalStatus;
            return operationalStatus.Length > 1 ? operationalStatus[1] : null;
        }
    }

    public string SecondaryStatusDescription
    {
        get
        {
            var statusDescription = StatusDescription;
            if (statusDescription.Length > 1)
                return statusDescription[1];
            return null;
        }
    }

    public string[] StatusDescription { get; set; }
}
