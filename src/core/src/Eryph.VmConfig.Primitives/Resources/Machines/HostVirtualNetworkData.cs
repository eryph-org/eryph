using System;

namespace Eryph.Resources.Machines;

public class HostVirtualNetworkData : MachineNetworkData
{
    public Guid? VirtualSwitchId{ get; set; }
    public string DeviceId{ get; set; }
    public string AdapterId { get; set; }


}