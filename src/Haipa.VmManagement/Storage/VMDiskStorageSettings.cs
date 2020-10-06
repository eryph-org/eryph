using LanguageExt;

namespace Haipa.VmManagement.Storage
{
    public class VMDiskStorageSettings : VMDriveStorageSettings
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public Option<string> ParentPath { get; set; }
        public Option<string> AttachPath { get; set; }

        public Option<string> StorageIdentifier { get; set; }
        public StorageNames StorageNames { get; set; }
        public long SizeBytes { get; set; }

    }
}