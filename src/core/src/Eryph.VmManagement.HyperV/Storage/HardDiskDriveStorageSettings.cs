using LanguageExt;

namespace Eryph.VmManagement.Storage
{
    public class HardDiskDriveStorageSettings : VMDriveStorageSettings
    {
        public Option<string> AttachPath { get; set; }

        public DiskStorageSettings DiskSettings { get; set; }
    }
}