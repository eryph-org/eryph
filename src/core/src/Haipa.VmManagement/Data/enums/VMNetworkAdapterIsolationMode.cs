namespace Haipa.VmManagement.Data
{
    public enum VMNetworkAdapterIsolationMode : byte
    {
        None,
        NativeVirtualSubnet,
        ExternalVirtualSubnet,
        Vlan,
    }
}