namespace Eryph.VmManagement.Data.enums;

public enum VMNetworkAdapterIsolationMode : byte
{
    None,
    NativeVirtualSubnet,
    ExternalVirtualSubnet,
    Vlan,
}
