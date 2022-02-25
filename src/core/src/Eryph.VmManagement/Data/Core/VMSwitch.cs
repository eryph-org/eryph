using Eryph.Core;

namespace Eryph.VmManagement.Data.Core;

public class VMSwitch
{
    [PrivateIdentifier]

    public string Id { get; private set; }

    [PrivateIdentifier]
    public string Name { get; private set; }
}