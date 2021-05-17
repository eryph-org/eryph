namespace Haipa.VmManagement.Data
{
    public sealed class VMNetworkAdapterAclSetting
    {
        public VMNetworkAdapterAclAction Action { get; private set; }

        public VMNetworkAdapterAclDirection Direction { get; private set; }

        public string LocalAddress { get; private set; }

        public VMNetworkAdapterAclType LocalAddressType { get; private set; }

        public string MeteredMegabytes { get; private set; }

        public string RemoteAddress { get; private set; }

        public VMNetworkAdapterAclType RemoteAddressType { get; private set; }
    }
}