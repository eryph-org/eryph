namespace Eryph.Messages.Resources.Catlets.Commands
{
    // Read from the guest's Hyper-V KVP pool. Provisioning state mirrors the
    // single eryph.provisioning.state value (set natively on Windows, mirrored
    // from cloud-init on Linux). Any field is null when its KVP key is absent.
    public class GetGuestServicesStatusVMCommandResponse
    {
        public string GuestServicesStatus { get; set; }
        public string GuestServicesVersion { get; set; }
        public string ProvisioningState { get; set; }
        public string Shell { get; set; }
    }
}
