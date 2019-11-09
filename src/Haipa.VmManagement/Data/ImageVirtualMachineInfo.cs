using LanguageExt;

namespace Haipa.VmManagement.Data
{
    public sealed class ImageVirtualMachineInfo
    {
        public DvdDriveInfo[] DVDDrives { get; set; }

        public VMFibreChannelHbaInfo[] FibreChannelHostBusAdapters { get; set; }

        public VMFloppyDiskDriveInfo FloppyDrive { get; set; }

        public ImageHardDiskDriveInfo[] HardDrives { get; set; }

        public VMNetworkAdapter[] NetworkAdapters { get;  set; }

        public int Generation { get; set; }

        public long ProcessorCount { get; set; }

        public long MemoryMaximum { get; set; }

        public long MemoryMinimum { get; set; }

        public long MemoryStartup { get; set; }


    }


    public sealed class ImageHardDiskDriveInfo : DriveInfo
    {
        public int? DiskNumber { get; set; }

        public ulong? MaximumIOPS { get; set; }

        public ulong? MinimumIOPS { get; set; }

        //public Guid? QoSPolicyID { get; set; }

        public bool? SupportPersistentReservations { get; set; }

        //public CacheAttributes? WriteHardeningMethod { get; set; }

        public AttachedDiskType AttachedDiskType { get; set; }

        public long Size { get; set;  }

    }
}