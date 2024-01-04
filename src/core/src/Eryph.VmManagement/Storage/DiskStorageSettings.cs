using System;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
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

        public long? SizeBytes { get; set; }
        public long? SizeBytesCreate { get; set; }


        public static Option<DiskStorageSettings> FromSourceString(
            VmHostAgentConfiguration vmHostAgentConfig,
            HostSettings hostSettings,
            string templateString)
        {
            if (!templateString.StartsWith("gene:"))
            {
                var (storageNames, storageIdentifier) = StorageNames.FromPath(templateString,
                    vmHostAgentConfig, hostSettings.DefaultVirtualHardDiskPath);
                return new DiskStorageSettings
                {
                    StorageNames = storageNames,
                    StorageIdentifier = storageIdentifier,
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

            var genesetName = parts[1].Replace('/', '\\');
            var diskName = "sda";

            if (parts.Length == 3)
            {
                diskName = parts[2];
            }

            var geneDiskPath = System.IO.Path.Combine(hostSettings.DefaultVirtualHardDiskPath,
                "genepool", genesetName, "volumes", $"{diskName}.vhdx");
            var (geneDiskStorageNames, geneDiskStorageIdentifier) = StorageNames.FromPath(geneDiskPath,
                vmHostAgentConfig, hostSettings.DefaultVirtualHardDiskPath);

            return new DiskStorageSettings
            {
                StorageNames = geneDiskStorageNames,
                StorageIdentifier = geneDiskStorageIdentifier,
                Path = System.IO.Path.GetDirectoryName(geneDiskPath),
                FileName = System.IO.Path.GetFileName(geneDiskPath),
                Name = System.IO.Path.GetFileNameWithoutExtension(geneDiskPath)
            };

        }

        public static Task<Either<PowershellFailure, Option<DiskStorageSettings>>> FromVhdPath(
            IPowershellEngine engine,
            VmHostAgentConfiguration vmHostAgentConfig,
            HostSettings hostSettings,
            Option<string> optionalPath)
        {
            return optionalPath.Map(path => from optionalVhdInfo in VhdQuery.GetVhdInfo(engine, path).ToAsync()
                from vhdInfo in optionalVhdInfo.ToEither(new PowershellFailure {Message = "Failed to read VHD "})
                    .ToAsync()
                let nameAndId = StorageNames.FromPath(System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                    vmHostAgentConfig, hostSettings.DefaultVirtualHardDiskPath)
                let parentPath = string.IsNullOrWhiteSpace(vhdInfo.Value.ParentPath)
                    ? Option<string>.None
                    : Option<string>.Some(vhdInfo.Value.ParentPath)
                from parentSettings in FromVhdPath(engine, vmHostAgentConfig, hostSettings, parentPath).ToAsync()
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