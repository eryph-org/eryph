using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Full;

public class HostVMNetworkAdapter : VMNetworkAdapter
{
    [PrivateIdentifier]
    public string DeviceId { get; private set; }
}