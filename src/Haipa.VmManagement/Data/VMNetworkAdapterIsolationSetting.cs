namespace Haipa.VmManagement.Data
{
    public sealed class VMNetworkAdapterIsolationSetting
    {
        public VMNetworkAdapterIsolationMode IsolationMode { get; private set; }

        public bool AllowUntaggedTraffic { get; private set; }

        public int DefaultIsolationID { get; private set; }

        public OnOffState MultiTenantStack { get; private set; }

    }
}