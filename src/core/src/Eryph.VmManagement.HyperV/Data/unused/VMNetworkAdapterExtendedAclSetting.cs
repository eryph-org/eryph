namespace Eryph.VmManagement.Data
{
    public sealed class VMNetworkAdapterExtendedAclSetting
    {
        public VMNetworkAdapterExtendedAclDirection Direction { get; private set; }

        public VMNetworkAdapterExtendedAclAction Action { get; private set; }

        public string LocalIPAddress { get; private set; }

        public string RemoteIPAddress { get; private set; }

        public string LocalPort { get; private set; }

        public string RemotePort { get; private set; }

        public string Protocol { get; private set; }

        public int Weight { get; private set; }

        public bool Stateful { get; private set; }

        public int IdleSessionTimeout { get; private set; }

        public int IsolationID { get; private set; }
    }
}