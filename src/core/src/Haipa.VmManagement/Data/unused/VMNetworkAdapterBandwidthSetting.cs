namespace Haipa.VmManagement.Data
{
    public sealed class VMNetworkAdapterBandwidthSetting
    {
        public long? MinimumBandwidthAbsolute { get; private set; }


        public long? MinimumBandwidthWeight { get; private set; }


        public long? MaximumBandwidth { get; private set; }
    }
}