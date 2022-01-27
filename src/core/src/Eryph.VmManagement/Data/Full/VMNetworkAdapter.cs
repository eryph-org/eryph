using System;
using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data.Full
{
    public class VMNetworkAdapter : VirtualMachineDeviceInfo, IVMNetworkAdapterWithConnection
    {
        //public bool DynamicMacAddressEnabled { get; private set; }

        //public bool AllowPacketDirect { get; private set; }

        //public bool IsLegacy { get; private set; }
        //public string[] IPAddresses { get; private set; }

        //public OnOffState DeviceNaming { get; private set; }

        //public uint IovWeight { get; private set; }

        //public uint IovQueuePairsRequested { get; private set; }

        //public IovInterruptModerationValue IovInterruptModeration { get; private set; }

        //public uint PacketDirectNumProcs { get; private set; }

        //public uint PacketDirectModerationCount { get; private set; }

        //public uint PacketDirectModerationInterval { get; private set; }

        //cause nullReference exception
        //public uint IovQueuePairsAssigned { get; private set; }

        //public int IovUsage { get; private set; }

        //public string[] MandatoryFeatureId { get; private set; }

        //public string[] MandatoryFeatureName { get; private set; }

        //public string PoolName { get; private set; }

        public bool Connected { get; private set; }

        //public bool ClusterMonitored { get; private set; }

        public string MacAddress { get; private set; }

        //public string TestReplicaPoolName { get; private set; }

        //public string TestReplicaSwitchName { get; private set; }

        public string SwitchName { get; private set; }

        //public string AdapterId { get; private set; }

        //public string[] StatusDescription { get; private set; }


        //public VMNetworkAdapterOperationalStatus[] Status { get; private set; }


        //public bool IsManagementOs { get; private set; }

        //public bool IsExternalAdapter { get; private set; }

        public Guid? SwitchId { get; private set; }


        //public VMNetworkAdapterAclSetting[] AclList { get; private set; }


        //public VMNetworkAdapterExtendedAclSetting[] ExtendedAclList { get; private set; }


        //public VMNetworkAdapterIsolationSetting IsolationSetting { get; private set; }

        //public VMNetworkAdapterRoutingDomainSetting[] RoutingDomainList { get; private set; }


        public VMNetworkAdapterVlanSetting VlanSetting { get; private set; }


        //public VMNetworkAdapterBandwidthSetting BandwidthSetting { get; private set; }


        //public VMNetworkAdapterIsolationMode CurrentIsolationMode { get; private set; }


        //public OnOffState MacAddressSpoofing { get; private set; }


        //public OnOffState DhcpGuard { get; private set; }


        //public OnOffState RouterGuard { get; private set; }


        //public VMNetworkAdapterPortMirroringMode PortMirroringMode { get; private set; }


        //public OnOffState IeeePriorityTag { get; private set; }


        //public uint VirtualSubnetId { get; private set; }


        //public uint DynamicIPAddressLimit { get; private set; }


        //public uint StormLimit { get; private set; }


        //public OnOffState AllowTeaming { get; private set; }


        ////public OnOffState FixSpeed10G { get; private set; }


        //public uint VMQWeight { get; private set; }


        //public long IPsecOffloadMaxSA { get; private set; }


        //public bool VrssEnabled { get; private set; }


        //public bool VrssEnabledRequested { get; private set; }


        //public bool VmmqEnabled { get; private set; }


        //public bool VmmqEnabledRequested { get; private set; }


        //public uint VrssMaxQueuePairs { get; private set; }


        //public uint VrssMaxQueuePairsRequested { get; private set; }


        //public uint VrssMinQueuePairs { get; private set; }


        //public uint VrssMinQueuePairsRequested { get; private set; }


        //public VrssQueueSchedulingModeType VrssQueueSchedulingMode { get; private set; }


        //public VrssQueueSchedulingModeType VrssQueueSchedulingModeRequested { get; private set; }


        //public bool VrssExcludePrimaryProcessor { get; private set; }


        //public bool VrssExcludePrimaryProcessorRequested { get; private set; }


        //public bool VrssIndependentHostSpreading { get; private set; }


        //public bool VrssIndependentHostSpreadingRequested { get; private set; }

        //public VrssVmbusChannelAffinityPolicyType VrssVmbusChannelAffinityPolicy { get; private set; }

        //public VrssVmbusChannelAffinityPolicyType VrssVmbusChannelAffinityPolicyRequested { get; private set; }

        //public int VmqUsage { get; private set; }


        //public uint IPsecOffloadSAUsage { get; private set; }


        //public bool VFDataPathActive { get; private set; }

        //public uint BandwidthPercentage { get; private set; }
    }
}