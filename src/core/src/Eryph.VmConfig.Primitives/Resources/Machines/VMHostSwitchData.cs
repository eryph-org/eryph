using Eryph.ConfigModel;
using Eryph.Core;

namespace Eryph.Resources.Machines;

public class VMHostSwitchData
{
    [PrivateIdentifier]

    public string Id { get; set; }

    [PrivateIdentifier]
    public string VirtualSwitchName { get; set; }
}