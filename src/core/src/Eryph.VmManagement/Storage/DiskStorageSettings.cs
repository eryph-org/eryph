using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.VmAgent;
using Eryph.GenePool.Model;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage
{
    public class DiskStorageSettings
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public int Generation { get; set; }
        public Guid DiskIdentifier { get; set; }

        public Option<DiskStorageSettings> ParentSettings { get; set; }

        public Option<string> StorageIdentifier { get; set; }
        public StorageNames StorageNames { get; set; }

        public long? UsedSizeBytes { get; set; }

        public long? SizeBytes { get; set; }
        public long? SizeBytesCreate { get; set; }
        public Option<GeneSetIdentifier> Geneset { get; set; }

        public static Option<DiskStorageSettings> FromSourceString(
            VmHostAgentConfiguration vmHostAgentConfig,
            string templateString)
        {
            if (!templateString.StartsWith("gene:"))
            {
                var (storageNames, storageIdentifier) = StorageNames.FromVhdPath(templateString, vmHostAgentConfig);
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

            var geneDiskPath = System.IO.Path.Combine(vmHostAgentConfig.Defaults.Volumes,
                "genepool", genesetName, "volumes", $"{diskName}.vhdx");
            var (geneDiskStorageNames, geneDiskStorageIdentifier) = StorageNames.FromVhdPath(geneDiskPath, vmHostAgentConfig);

            return new DiskStorageSettings
            {
                StorageNames = geneDiskStorageNames,
                StorageIdentifier = geneDiskStorageIdentifier,
                Path = System.IO.Path.GetDirectoryName(geneDiskPath),
                FileName = System.IO.Path.GetFileName(geneDiskPath),
                Generation = 0,
                Name = System.IO.Path.GetFileNameWithoutExtension(geneDiskPath)
            };

        }

        public static EitherAsync<Error, DiskStorageSettings> FromVhdPath(
            IPowershellEngine engine,
            VmHostAgentConfiguration vmHostAgentConfig,
            string path) =>
            from optionalVhdInfo in VhdQuery.GetVhdInfo(engine, path).ToAsync().ToError()
            from vhdInfo in optionalVhdInfo.ToEitherAsync(
                Error.New("Failed to read VHD"))
            let nameAndId = StorageNames.FromVhdPath(vhdInfo.Value.Path,
                vmHostAgentConfig)
            let parentPath = Optional(vhdInfo.Value.ParentPath).Filter(notEmpty)
            from parentSettings in parentPath.Map(p => FromVhdPath(engine, vmHostAgentConfig, p))
                .Sequence()
            let generation = parentSettings.Map(p => p.Generation).IfNone(-1) + 1
            select
                new DiskStorageSettings
                {
                    Path = System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                    Name = GetNameWithoutGeneration(vhdInfo.Value.Path, generation),
                    FileName = System.IO.Path.GetFileName(vhdInfo.Value.Path),
                    StorageNames = nameAndId.Names,
                    StorageIdentifier = nameAndId.StorageIdentifier,
                    Geneset = nameAndId.StorageIdentifier.Bind(GeneIdentifier.NewOption).Map(g => g.GeneSet),
                    SizeBytes = vhdInfo.Value.Size,
                    UsedSizeBytes = vhdInfo.Value.FileSize,
                    DiskIdentifier = vhdInfo.Value.DiskIdentifier,
                    Generation = generation,
                    ParentSettings = parentSettings
                };
        

        private static string GetNameWithoutGeneration(string path, int generation)
        {
            if (generation == 0)
                return System.IO.Path.GetFileNameWithoutExtension(path);

            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            var generationSuffix = $"_g{generation}";
            return fileName.EndsWith(generationSuffix)
                ? fileName[..^generationSuffix.Length]
                : fileName;
        }
    }
}