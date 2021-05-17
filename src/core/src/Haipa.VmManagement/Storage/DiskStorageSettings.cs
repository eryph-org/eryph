using System.Threading.Tasks;
using Haipa.Primitives;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.VmManagement.Storage
{
    public class DiskStorageSettings
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }

        public Option<DiskStorageSettings> ParentSettings { get; set; }

        public Option<string> StorageIdentifier { get; set; }
        public StorageNames StorageNames { get; set; }

        public long SizeBytes { get; set; }


        public static Task<Either<PowershellFailure, Option<DiskStorageSettings>>> FromVhdPath(IPowershellEngine engine, HostSettings hostSettings, Option<string> optionalPath)
        {
            return optionalPath.Map(path => from optionalVhdInfo in VhdQuery.GetVhdInfo(engine, path).ToAsync()

                from vhdInfo in optionalVhdInfo.ToEither(new PowershellFailure{ Message = "Failed to read VHD "}).ToAsync()
                let nameAndId = StorageNames.FromPath(System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                    hostSettings.DefaultVirtualHardDiskPath)
                let parentPath = string.IsNullOrWhiteSpace(vhdInfo.Value.ParentPath) ? Option<string>.None : Option<string>.Some(vhdInfo.Value.ParentPath)
                from parentSettings in FromVhdPath(engine, hostSettings, parentPath).ToAsync()
                select
                    new DiskStorageSettings
                    {
                        Path = System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                        Name = System.IO.Path.GetFileNameWithoutExtension(vhdInfo.Value.Path),
                        FileName = System.IO.Path.GetFileName(vhdInfo.Value.Path),
                        StorageNames = nameAndId.Names,
                        StorageIdentifier = nameAndId.StorageIdentifier,
                        SizeBytes = vhdInfo.Value.Size,
                        ParentSettings = parentSettings
                    }).Traverse(l => l).ToEither();

        }
    }


}