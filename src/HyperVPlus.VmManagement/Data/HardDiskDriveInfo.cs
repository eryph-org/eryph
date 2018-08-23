namespace HyperVPlus.VmManagement.Data
{
    public sealed class HardDiskDriveInfo : DriveInfo
    {
        public int? DiskNumber { get; set; }

        public ulong? MaximumIOPS { get; set; }

        public ulong? MinimumIOPS { get; set; }

        //public Guid? QoSPolicyID { get; set; }

        public bool? SupportPersistentReservations { get; set; }

        //public CacheAttributes? WriteHardeningMethod { get; set; }

        internal AttachedDiskType AttachedDiskType { get; set; }

    }
}