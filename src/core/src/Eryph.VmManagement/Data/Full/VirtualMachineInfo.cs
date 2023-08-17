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
        //public string ComputerName { get; private set; }


        //public string ConfigurationLocation { get; private set; }

        //public bool SmartPagingFileInUse { get; private set; }

        //public string SmartPagingFilePath { get; private set; }

        //public string SnapshotFileLocation { get; private set; }

        //public StartAction AutomaticStartAction { get; private set; }


        //public int AutomaticStartDelay { get; private set; }


        //public StopAction AutomaticStopAction { get; private set; }


        //public CriticalErrorAction AutomaticCriticalErrorAction { get; private set; }


        //public int AutomaticCriticalErrorActionTimeout { get; private set; }


        public bool AutomaticCheckpointsEnabled { get; private set; }


        //public int CPUUsage { get; private set; }


        //public long MemoryAssigned { get; private set; }


        //public long MemoryDemand { get; private set; }


        //public string MemoryStatus { get; private set; }


        //public bool? NumaAligned { get; private set; }


        //public int NumaNodesCount { get; private set; }


        //public int NumaSocketCount { get; private set; }


        //public VMHeartbeatStatus? Heartbeat { get; private set; }


        //public string IntegrationServicesState { get; private set; }


        //public Version IntegrationServicesVersion { get; private set; }


        public TimeSpan Uptime { get; private set; }


        //public VirtualMachineOperationalStatus[] OperationalStatus { get; private set; }


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

        //public VirtualMachineOperationalStatus? SecondaryOperationalStatus { get; private set; }


        //public string[] StatusDescriptions { get; private set; }


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

        //public string Status { get; private set; }

        //public VMReplicationHealthState ReplicationHealth { get; private set; }

        //public VMReplicationMode ReplicationMode { get; private set; }


        //public VMReplicationState ReplicationState { get; private set; }


        //public bool ResourceMeteringEnabled { get; private set; }


        public CheckpointType CheckpointType { get; private set; }

        public bool DynamicMemoryEnabled { get; private set; }


        public long MemoryMaximum { get; private set; }

        public long MemoryMinimum { get; private set; }


        //public bool IsClustered { get; private set; }

        [PrivateIdentifier]
        public string Notes { get; private set; }

        //public DateTime CreationTime { get; private set; }

        [PrivateIdentifier]
        public Guid Id { get; private set; }

        [PrivateIdentifier]
        public string Name { get; private set; }


        public long MemoryStartup { get; private set; }


        public long ProcessorCount { get; private set; }


        //public bool BatteryPassthroughEnabled { get; private set; }


        public int Generation { get; private set; }

        //public Guid? ParentSnapshotId { get; private set; }


        //public string ParentSnapshotName { get; private set; }

        [PrivateIdentifier]
        public string Path { get; private set; }

        //public VMGroupInfo[] Groups { get; private set; }


        //public VirtualMachineType VirtualMachineType { get; private set; }


        //public VirtualMachineSubType VirtualMachineSubType { get; private set; }

        //public VMComPortInfo ComPort1 { get; private set; }


        //public VMComPortInfo ComPort2 { get; private set; }

        public VirtualMachineDeviceInfo[] DVDDrives { get; private set; }


        //public VMFibreChannelHbaInfo[] FibreChannelHostBusAdapters { get; private set; }


        //public VMFloppyDiskDriveInfo FloppyDrive { get; private set; }


        public VirtualMachineDeviceInfo[] HardDrives { get; private set; }


        //public VMRemoteFx3DVideoAdapterInfo RemoteFxAdapter { get; private set; }


        //public VirtualMachineIntegrationComponentInfo[] VMIntegrationService { get; private set; }

        public VirtualMachineDeviceInfo[] NetworkAdapters { get; private set; }


        //public long SizeOfSystemFiles { get; private set; }


        public VirtualMachineState State { get; private set; }

        //public string Version { get; private set; }


        //public bool GuestControlledCacheTypes { get; private set; }


        //public uint LowMemoryMappedIoSpace { get; private set; }


        //public ulong HighMemoryMappedIoSpace { get; private set; }


        //public OnOffState? LockOnDisconnect { get; private set; }
    }
}