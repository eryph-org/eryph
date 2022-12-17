using System;
using Eryph.ConfigModel;
using Eryph.Core;

namespace Eryph.Resources.Machines;

public class HostVirtualNetworkData : MachineNetworkData
{
    [PrivateIdentifier]
    public Guid? VirtualSwitchId{ get; set; }

    [PrivateIdentifier]
    public string DeviceId{ get; set; }

    [PrivateIdentifier]
    public string AdapterId { get; set; }


}