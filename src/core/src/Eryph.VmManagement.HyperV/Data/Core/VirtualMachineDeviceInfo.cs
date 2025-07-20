using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core;

public class VirtualMachineDeviceInfo
{
    [PrivateIdentifier]
    public virtual string Name { get; init; }

    [PrivateIdentifier]
    public virtual string Id { get; init; }
}
