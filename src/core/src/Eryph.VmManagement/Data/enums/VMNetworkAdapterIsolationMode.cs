namespace Eryph.VmManagement.Data
{
    public enum VMNetworkAdapterIsolationMode : byte
    {
        None,
        NativeVirtualSubnet,
        ExternalVirtualSubnet,
        Vlan
    }
}