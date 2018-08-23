using System;
using LanguageExt;

// ReSharper disable UnusedAutoPropertyAccessor.Local

// ReSharper disable InconsistentNaming

// ReSharper disable UnusedMember.Global

namespace HyperVPlus.Agent.Management.Data
{
    public sealed class VirtualMachineInfo : Record<VirtualMachineInfo>
    {

        public DateTime CreationTime { get; private set; }

        public Guid Id { get; private set; }

        public string Name { get; private set; }

        public string ComputerName { get; private set; }


        public string ConfigurationLocation { get; private set; }

        public bool SmartPagingFileInUse { get; private set; }

        public string SmartPagingFilePath { get; private set; }

        public string SnapshotFileLocation { get; private set; }

        public StartAction AutomaticStartAction { get; private set; }


        public int AutomaticStartDelay { get; private set; }


        public StopAction AutomaticStopAction { get; private set; }


        public CriticalErrorAction AutomaticCriticalErrorAction { get; private set; }


        public int AutomaticCriticalErrorActionTimeout { get; private set; }


        public bool AutomaticCheckpointsEnabled { get; private set; }


        public int CPUUsage { get; private set; }


        public long MemoryAssigned { get; private set; }


        public long MemoryDemand { get; private set; }


        public string MemoryStatus { get; private set; }


        public bool? NumaAligned { get; private set; }


        public int NumaNodesCount { get; private set; }


        public int NumaSocketCount { get; private set; }


        public VMHeartbeatStatus? Heartbeat { get; private set; }


        public string IntegrationServicesState { get; private set; }


        public Version IntegrationServicesVersion { get; private set; }


        public TimeSpan Uptime { get; private set; }


        public VirtualMachineOperationalStatus[] OperationalStatus { get; private set; }


        public VirtualMachineOperationalStatus? PrimaryOperationalStatus
        {
            get
            {
                var operationalStatus = OperationalStatus;
                if (operationalStatus != null && operationalStatus.Length != 0)
                    return operationalStatus[0];
                return new VirtualMachineOperationalStatus?();
            }
        }

        public VirtualMachineOperationalStatus? SecondaryOperationalStatus { get; private set; }


        public string[] StatusDescriptions { get; private set; }


        public string PrimaryStatusDescription
        {
            get
            {
                var statusDescriptions = StatusDescriptions;
                if (statusDescriptions != null && statusDescriptions.Length != 0)
                    return statusDescriptions[0];
                return null;
            }
        }

        public string SecondaryStatusDescription
        {
            get
            {
                var statusDescriptions = StatusDescriptions;
                if (statusDescriptions != null && statusDescriptions.Length > 1)
                    return statusDescriptions[1];
                return null;
            }
        }

        public string Status { get; private set; }

        public VMReplicationHealthState ReplicationHealth { get; private set; }

        public VMReplicationMode ReplicationMode { get; private set; }


        public VMReplicationState ReplicationState { get; private set; }


        public bool ResourceMeteringEnabled { get; private set; }


        public CheckpointType CheckpointType { get; private set; }

        public VMGroupInfo[] Groups { get; private set; }


        public VirtualMachineType VirtualMachineType { get; private set; }


        public VirtualMachineSubType VirtualMachineSubType { get; private set; }

        public VMComPortInfo ComPort1 { get; private set; }


        public VMComPortInfo ComPort2 { get; private set; }

        public DvdDriveInfo[] DVDDrives { get; private set; }


        public VMFibreChannelHbaInfo[] FibreChannelHostBusAdapters { get; private set; }


        public VMFloppyDiskDriveInfo FloppyDrive { get; private set; }


        public HardDiskDriveInfo[] HardDrives { get; private set; }


        public VMRemoteFx3DVideoAdapterInfo RemoteFxAdapter { get; private set; }


        public VirtualMachineIntegrationComponentInfo[] VMIntegrationService { get; private set; }

        public VMNetworkAdapter[] NetworkAdapters { get; private set; }

        public bool DynamicMemoryEnabled { get; private set; }


        public long MemoryMaximum { get; private set; }


        public long MemoryMinimum { get; private set; }


        public long MemoryStartup { get; private set; }


        public long ProcessorCount { get; private set; }


        public bool BatteryPassthroughEnabled { get; private set; }


        public int Generation { get; private set; }


        public bool IsClustered { get; private set; }

        public string Notes { get; private set; }

        //public Guid? ParentSnapshotId { get; private set; }


        //public string ParentSnapshotName { get; private set; }


        public string Path { get; private set; }


        public long SizeOfSystemFiles { get; private set; }


        public VirtualMachineState State { get; private set; }

        public string Version { get; private set; }


        //public bool GuestControlledCacheTypes { get; private set; }


        //public uint LowMemoryMappedIoSpace { get; private set; }


        //public ulong HighMemoryMappedIoSpace { get; private set; }


        //public OnOffState? LockOnDisconnect { get; private set; }



    }


    public class VMNetworkAdapter : VirtualMachineDeviceInfo
    {

        public bool ClusterMonitored { get; private set; }

        public string MacAddress { get; private set; }

        public bool DynamicMacAddressEnabled { get; private set; }

        //public bool AllowPacketDirect { get; private set; }

        public bool IsLegacy { get; private set; }
        public string[] IPAddresses { get; private set; }

        //public OnOffState DeviceNaming { get; private set; }

        public uint IovWeight { get; private set; }

        public uint IovQueuePairsRequested { get; private set; }

        public IovInterruptModerationValue IovInterruptModeration { get; private set; }

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

        //public string TestReplicaPoolName { get; private set; }

        //public string TestReplicaSwitchName { get; private set; }

        public string SwitchName { get; private set; }

        public string AdapterId { get; private set; }

        public string[] StatusDescription { get; private set; }


        public VMNetworkAdapterOperationalStatus[] Status { get; private set; }


        //public bool IsManagementOs { get; private set; }

        //public bool IsExternalAdapter { get; private set; }

        public Guid? SwitchId { get; private set; }


        public VMNetworkAdapterAclSetting[] AclList { get; private set; }


        public VMNetworkAdapterExtendedAclSetting[] ExtendedAclList { get; private set; }


        public VMNetworkAdapterIsolationSetting IsolationSetting { get; private set; }

        public VMNetworkAdapterRoutingDomainSetting[] RoutingDomainList { get; private set; }


        public VMNetworkAdapterVlanSetting VlanSetting { get; private set; }


        public VMNetworkAdapterBandwidthSetting BandwidthSetting { get; private set; }



        public VMNetworkAdapterIsolationMode CurrentIsolationMode { get; private set; }


        public OnOffState MacAddressSpoofing { get; private set; }


        public OnOffState DhcpGuard { get; private set; }


        public OnOffState RouterGuard { get; private set; }


        public VMNetworkAdapterPortMirroringMode PortMirroringMode { get; private set; }


        public OnOffState IeeePriorityTag { get; private set; }


        public uint VirtualSubnetId { get; private set; }


        public uint DynamicIPAddressLimit { get; private set; }


        public uint StormLimit { get; private set; }


        public OnOffState AllowTeaming { get; private set; }


        //public OnOffState FixSpeed10G { get; private set; }


        public uint VMQWeight { get; private set; }


        public long IPsecOffloadMaxSA { get; private set; }


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


        public bool VFDataPathActive { get; private set; }

        public uint BandwidthPercentage { get; private set; }

    }

    public class VMFirmwareInfo
    {
        public VMBootSourceInfo[] BootOrder { get; private set; }

        public IPProtocolPreference PreferredNetworkBootProtocol { get; private set; }


        public OnOffState SecureBoot { get; private set; }


        public Guid? SecureBootTemplateId { get; private set; }

        public ConsoleModeType ConsoleMode { get; private set; }


        public OnOffState PauseAfterBootFailure { get; private set; }


    }

    public enum IPProtocolPreference
    {
        IPv4,
        IPv6,
    }

    public enum ConsoleModeType
    {
        Default,
        COM1,
        COM2,
        None,
    }

    public class VMBootSourceInfo : Record<VMBootSourceInfo>
    {

        public VMBootSourceType BootType { get; private set; }

        public string Description { get; private set; }


    }

    public enum VMBootSourceType
    {
        Unknown,
        Drive,
        Network,
        File,
    }

    public enum VrssVmbusChannelAffinityPolicyType
    {
        None = 1,
        Weak = 2,
        Strong = 3,
        Strict = 4,
    }

    public enum VrssQueueSchedulingModeType
    {
        Dynamic,
        StaticVmq,
        StaticVrss,
    }

    public enum VMNetworkAdapterPortMirroringMode
    {
        None,
        Destination,
        Source,
    }

    public enum VMNetworkAdapterIsolationMode : byte
    {
        None,
        NativeVirtualSubnet,
        ExternalVirtualSubnet,
        Vlan,
    }

    public enum IovInterruptModerationValue
    {
        Default = 0,
        Adaptive = 1,
        Off = 2,
        Low = 100, // 0x00000064
        Medium = 200, // 0x000000C8
        High = 300, // 0x0000012C
    }

    public enum VMNetworkAdapterOperationalStatus
    {
        Unknown = 0,
        Other = 1,
        Ok = 2,
        Degraded = 3,
        Stressed = 4,
        PredictiveFailure = 5,
        Error = 6,
        NonRecoverableError = 7,
        Starting = 8,
        Stopping = 9,
        Stopped = 10, // 0x0000000A
        InService = 11, // 0x0000000B
        NoContact = 12, // 0x0000000C
        LostCommunication = 13, // 0x0000000D
        Aborted = 14, // 0x0000000E
        Dormant = 15, // 0x0000000F
        SupportingEntity = 16, // 0x00000010
        Completed = 17, // 0x00000011
        PowerMode = 18, // 0x00000012
        ProtocolVersion = 32775, // 0x00008007
    }

    public sealed class VMNetworkAdapterRoutingDomainSetting
    {
        public Guid RoutingDomainID { get; private set; }

        public string RoutingDomainName { get; private set; }
        public int[] IsolationID { get; private set; }

        public string[] IsolationName { get; private set; }

    }

    public sealed class VMNetworkAdapterBandwidthSetting
    {
        public long? MinimumBandwidthAbsolute { get; private set; }


        public long? MinimumBandwidthWeight { get; private set; }


        public long? MaximumBandwidth { get; private set; }

    }

    public sealed class VMNetworkAdapterVlanSetting
    {


        public VMNetworkAdapterVlanMode OperationMode { get; private set; }


        public int AccessVlanId { get; private set; }


        public int NativeVlanId { get; private set; }


        public int[] AllowedVlanIdList { get; private set; }


        public string AllowedVlanIdListString { get; private set; }


        public VMNetworkAdapterPrivateVlanMode PrivateVlanMode { get; private set; }


        public int PrimaryVlanId { get; private set; }


        public int SecondaryVlanId { get; private set; }


        public int[] SecondaryVlanIdList { get; private set; }


        public string SecondaryVlanIdListString { get; private set; }

    }

    public sealed class VMNetworkAdapterIsolationSetting
    {
        public VMNetworkAdapterIsolationMode IsolationMode { get; private set; }

        public bool AllowUntaggedTraffic { get; private set; }

        public int DefaultIsolationID { get; private set; }

        public OnOffState MultiTenantStack { get; private set; }

    }


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

    public enum VMNetworkAdapterAclType : byte
    {
        Mac = 1,
        WildcardBoth = 6,
        WildcardMac = 7,
    }
    public enum VMNetworkAdapterAclAction : byte
    {
        Allow = 1,
        Deny = 2,
        Meter = 3,
    }
    public enum VMNetworkAdapterAclDirection : byte
    {
        Inbound = 1,
        Outbound = 2,
        Both = 3,
    }

    public enum VMNetworkAdapterExtendedAclDirection : byte
    {
        Inbound = 1,
        Outbound = 2,
    }

    public enum VMNetworkAdapterExtendedAclAction : byte
    {
        Allow = 1,
        Deny = 2,
    }

    public enum VMNetworkAdapterVlanMode
    {
        Untagged,
        Access,
        Trunk,
        Private,
    }

    public enum VMNetworkAdapterPrivateVlanMode
    {
        Isolated = 1,
        Community = 2,
        Promiscuous = 3,
    }
}