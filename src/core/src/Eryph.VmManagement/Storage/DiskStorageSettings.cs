using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement.Storage
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

        public static Option<DiskStorageSettings> FromTemplateString(HostSettings hostSettings, string templateString)
        {
            if (!templateString.StartsWith("image:"))
            {
                return new DiskStorageSettings
                {
                    StorageNames = StorageNames.FromPath(templateString,
                        hostSettings.DefaultVirtualHardDiskPath).Names,
                    StorageIdentifier = StorageNames.FromPath(templateString,
                        hostSettings.DefaultVirtualHardDiskPath).StorageIdentifier,
                    Path = System.IO.Path.GetDirectoryName(templateString),
                    FileName = System.IO.Path.GetFileName(templateString),
                    Name = System.IO.Path.GetFileNameWithoutExtension(templateString)
                };
            }

            var parts = templateString.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return Option<DiskStorageSettings>.None;
            }

            var imageName = parts[1].Replace('/', '\\');
            var diskName = "sda";

            if (parts.Length == 3)
            {
                diskName = parts[2];
            }

            var imageDiskPath = System.IO.Path.Combine(hostSettings.DefaultVirtualHardDiskPath,
                "images", imageName, "Virtual Hard Disks", $"{diskName}.vhdx");

            return new DiskStorageSettings
            {
                StorageNames = StorageNames.FromPath(imageDiskPath,
                    hostSettings.DefaultVirtualHardDiskPath).Names,
                StorageIdentifier = StorageNames.FromPath(imageDiskPath,
                    hostSettings.DefaultVirtualHardDiskPath).StorageIdentifier,
                Path = System.IO.Path.GetDirectoryName(imageDiskPath),
                FileName = System.IO.Path.GetFileName(imageDiskPath),
                Name = System.IO.Path.GetFileNameWithoutExtension(imageDiskPath)
            };

        }

        public static Task<Either<PowershellFailure, Option<DiskStorageSettings>>> FromVhdPath(IPowershellEngine engine,
            HostSettings hostSettings, Option<string> optionalPath)
        {
            return optionalPath.Map(path => from optionalVhdInfo in VhdQuery.GetVhdInfo(engine, path).ToAsync()
                from vhdInfo in optionalVhdInfo.ToEither(new PowershellFailure {Message = "Failed to read VHD "})
                    .ToAsync()
                let nameAndId = StorageNames.FromPath(System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                    hostSettings.DefaultVirtualHardDiskPath)
                let parentPath = string.IsNullOrWhiteSpace(vhdInfo.Value.ParentPath)
                    ? Option<string>.None
                    : Option<string>.Some(vhdInfo.Value.ParentPath)
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