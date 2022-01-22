using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data
{
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
                    : new VMIntegrationComponentOperationalStatus?();
            }
        }

        public string PrimaryStatusDescription
        {
            get
            {
                var statusDescription = StatusDescription;
                if (statusDescription.Length != 0)
                    return statusDescription[0];
                return null;
            }
        }

        public VMIntegrationComponentOperationalStatus? SecondaryOperationalStatus
        {
            get
            {
                var operationalStatus = OperationalStatus;
                if (operationalStatus.Length > 1)
                    return operationalStatus[1];
                return new VMIntegrationComponentOperationalStatus?();
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
}