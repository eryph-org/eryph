using LanguageExt;

namespace Haipa.VmManagement.Data
{
    public sealed class ImageVirtualMachineInfo : Record<ImageVirtualMachineInfo>
    {
        public DvdDriveInfo[] DVDDrives { get; private set; }

        public VMFibreChannelHbaInfo[] FibreChannelHostBusAdapters { get; private set; }

        public VMFloppyDiskDriveInfo FloppyDrive { get; private set; }

        public ImageHardDiskDriveInfo[] HardDrives { get; private set; }

        public VMNetworkAdapter[] NetworkAdapters { get; private set; }

        public int Generation { get; private set; }

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