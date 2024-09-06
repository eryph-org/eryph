using System;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
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

            return from geneId in GeneIdentifier.NewOption(templateString)
                   let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
                   let geneDiskPath = GenePoolPaths.GetGenePath(genePoolPath, GeneType.Volume, geneId)
                   let namesAndId = StorageNames.FromVhdPath(geneDiskPath, vmHostAgentConfig)
                   select new DiskStorageSettings
                   {
                       StorageNames = namesAndId.Names,
                       StorageIdentifier = namesAndId.StorageIdentifier,
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
            from vhdInfo in optionalVhdInfo.ToEitherAsync(Error.New(
                $"Could not read VHD {path}"))
            let nameAndId = StorageNames.FromVhdPath(vhdInfo.Value.Path,
                vmHostAgentConfig)
            let parentPath = Optional(vhdInfo.Value.ParentPath).Filter(notEmpty)
            from parentSettings in parentPath.Map(p => FromVhdPath(engine, vmHostAgentConfig, p))
                .Sequence()
            let generation = parentSettings.Map(p => p.Generation).IfNone(-1) + 1
            let geneId = nameAndId.StorageIdentifier.Bind(GeneIdentifier.NewOption)
            select
                new DiskStorageSettings
                {
                    Path = System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                    Name = GetNameWithoutGeneration(vhdInfo.Value.Path, generation),
                    FileName = System.IO.Path.GetFileName(vhdInfo.Value.Path),
                    StorageNames = nameAndId.Names,
                    StorageIdentifier = nameAndId.StorageIdentifier,
                    Geneset = geneId.Map(g => g.GeneSet),
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