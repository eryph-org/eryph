using System;
using Eryph.ConfigModel;
using Eryph.VmManagement.Data.Core;

// ReSharper disable UnusedAutoPropertyAccessor.Local

// ReSharper disable InconsistentNaming

// ReSharper disable UnusedMember.Global

namespace Eryph.VmManagement.Data.Full
{

    public sealed class VirtualMachineInfo :
        IVirtualMachineCoreInfo,
        IVMWithStateInfo,
        IVMWithNetworkAdapterInfo,
        IVMWithDrivesInfo
    {
        //public string ComputerName { get; init; }


        //public string ConfigurationLocation { get; init; }

        //public bool SmartPagingFileInUse { get; init; }

        //public string SmartPagingFilePath { get; init; }

        //public string SnapshotFileLocation { get; init; }

        //public StartAction AutomaticStartAction { get; init; }


        //public int AutomaticStartDelay { get; init; }


        //public StopAction AutomaticStopAction { get; init; }


        //public CriticalErrorAction AutomaticCriticalErrorAction { get; init; }


        //public int AutomaticCriticalErrorActionTimeout { get; init; }


        public bool AutomaticCheckpointsEnabled { get; init; }


        //public int CPUUsage { get; init; }


        //public long MemoryAssigned { get; init; }


        //public long MemoryDemand { get; init; }


        //public string MemoryStatus { get; init; }


        //public bool? NumaAligned { get; init; }


        //public int NumaNodesCount { get; init; }


        //public int NumaSocketCount { get; init; }


        //public VMHeartbeatStatus? Heartbeat { get; init; }


        //public string IntegrationServicesState { get; init; }


        //public Version IntegrationServicesVersion { get; init; }


        public TimeSpan Uptime { get; init; }


        public VirtualMachineOperationalStatus[] OperationalStatus { get; init; }


        //public VirtualMachineOperationalStatus? PrimaryOperationalStatus
        //{
        //    get
        //    {
        //        var operationalStatus = OperationalStatus;
        //        if (operationalStatus != null && operationalStatus.Length != 0)
        //            return operationalStatus[0];
        //        return new VirtualMachineOperationalStatus?();
        //    }
        //}

        //public VirtualMachineOperationalStatus? SecondaryOperationalStatus { get; init; }


        //public string[] StatusDescriptions { get; init; }


        //public string PrimaryStatusDescription
        //{
        //    get
        //    {
        //        var statusDescriptions = StatusDescriptions;
        //        if (statusDescriptions != null && statusDescriptions.Length != 0)
        //            return statusDescriptions[0];
        //        return null;
        //    }
        //}

        //public string SecondaryStatusDescription
        //{
        //    get
        //    {
        //        var statusDescriptions = StatusDescriptions;
        //        if (statusDescriptions != null && statusDescriptions.Length > 1)
        //            return statusDescriptions[1];
        //        return null;
        //    }
        //}

        //public string Status { get; init; }

        //public VMReplicationHealthState ReplicationHealth { get; init; }

        //public VMReplicationMode ReplicationMode { get; init; }


        //public VMReplicationState ReplicationState { get; init; }


        //public bool ResourceMeteringEnabled { get; init; }


        public CheckpointType CheckpointType { get; init; }

        public bool DynamicMemoryEnabled { get; init; }


        public long MemoryMaximum { get; init; }

        public long MemoryMinimum { get; init; }


        //public bool IsClustered { get; init; }

        [PrivateIdentifier]
        public string Notes { get; init; }

        //public DateTime CreationTime { get; init; }

        [PrivateIdentifier]
        public Guid Id { get; init; }

        [PrivateIdentifier]
        public string Name { get; init; }


        public long MemoryStartup { get; init; }


        public long ProcessorCount { get; init; }


        //public bool BatteryPassthroughEnabled { get; init; }


        public int Generation { get; init; }

        //public Guid? ParentSnapshotId { get; init; }


        //public string ParentSnapshotName { get; init; }

        [PrivateIdentifier]
        public string Path { get; init; }

        //public VMGroupInfo[] Groups { get; init; }


        //public VirtualMachineType VirtualMachineType { get; init; }


        //public VirtualMachineSubType VirtualMachineSubType { get; init; }

        //public VMComPortInfo ComPort1 { get; init; }


        //public VMComPortInfo ComPort2 { get; init; }

        public VirtualMachineDeviceInfo[] DVDDrives { get; init; }


        //public VMFibreChannelHbaInfo[] FibreChannelHostBusAdapters { get; init; }


        //public VMFloppyDiskDriveInfo FloppyDrive { get; init; }


        public VirtualMachineDeviceInfo[] HardDrives { get; init; }


        //public VMRemoteFx3DVideoAdapterInfo RemoteFxAdapter { get; init; }


        //public VirtualMachineIntegrationComponentInfo[] VMIntegrationService { get; init; }

        public VirtualMachineDeviceInfo[] NetworkAdapters { get; init; }


        //public long SizeOfSystemFiles { get; init; }


        public VirtualMachineState State { get; init; }

        //public string Version { get; init; }


        //public bool GuestControlledCacheTypes { get; init; }


        //public uint LowMemoryMappedIoSpace { get; init; }


        //public ulong HighMemoryMappedIoSpace { get; init; }


        //public OnOffState? LockOnDisconnect { get; init; }
    }
}