using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
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

        public Option<UniqueGeneIdentifier> Gene { get; set; }

        public static Option<DiskStorageSettings> FromSourceString(
            VmHostAgentConfiguration vmHostAgentConfig,
            Seq<UniqueGeneIdentifier> resolvedGenes,
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
                   from uniqueId in resolvedGenes.Find(g => g.GeneType == GeneType.Volume && g.Id == geneId)
                   let geneDiskPath = GenePoolPaths.GetGenePath(genePoolPath, uniqueId)
                   select new DiskStorageSettings
                   {
                       StorageNames = new StorageNames()
                       {
                           ProjectName = EryphConstants.DefaultProjectName,
                           EnvironmentName = EryphConstants.DefaultEnvironmentName,
                           DataStoreName = EryphConstants.DefaultDataStoreName,
                           ProjectId = EryphConstants.DefaultProjectId,
                       },
                       Gene = uniqueId,
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
            let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
            let vhdPath = vhdInfo.Value.Path
            from result in GenePoolPaths.IsPathInGenePool(genePoolPath, vhdPath)
                ? FromGeneVhdInfo(genePoolPath, vhdInfo.Value)
                : from _ in RightAsync<Error, Unit>(unit)
                  let nameAndId = StorageNames.FromVhdPath(vhdPath, vmHostAgentConfig)
                  let parentPath = Optional(vhdInfo.Value.ParentPath).Filter(notEmpty)
                  from parentSettings in parentPath.Map(p => FromVhdPath(engine, vmHostAgentConfig, p)).Sequence()
                  let generation = parentSettings.Map(p => p.Generation).IfNone(-1) + 1
                  select new DiskStorageSettings
                  {
                      Path = System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                      Name = GetNameWithoutGeneration(vhdInfo.Value.Path, generation),
                      FileName = System.IO.Path.GetFileName(vhdInfo.Value.Path),
                      StorageNames = nameAndId.Names,
                      StorageIdentifier = nameAndId.StorageIdentifier,
                      SizeBytes = vhdInfo.Value.Size,
                      UsedSizeBytes = vhdInfo.Value.FileSize,
                      DiskIdentifier = vhdInfo.Value.DiskIdentifier,
                      Generation = generation,
                      ParentSettings = parentSettings
                  }
            select result;

        private static EitherAsync<Error, DiskStorageSettings> FromGeneVhdInfo(
            string genePoolPath,
            VhdInfo vhdInfo) =>
            from uniqueGeneId in GenePoolPaths.GetUniqueGeneIdFromPath(genePoolPath, vhdInfo.Path)
                .ToAsync()
            select new DiskStorageSettings
            {
                Path = System.IO.Path.GetDirectoryName(vhdInfo.Path),
                Name = System.IO.Path.GetFileNameWithoutExtension(vhdInfo.Path),
                FileName = System.IO.Path.GetFileName(vhdInfo.Path),
                StorageNames = new StorageNames
                {
                    ProjectId = EryphConstants.DefaultProjectId,
                    ProjectName = EryphConstants.DefaultProjectName,
                    EnvironmentName = EryphConstants.DefaultEnvironmentName,
                    DataStoreName = EryphConstants.DefaultDataStoreName,
                },
                Gene = uniqueGeneId,
                SizeBytes = vhdInfo.Size,
                UsedSizeBytes = vhdInfo.FileSize,
                DiskIdentifier = vhdInfo.DiskIdentifier,
                Generation = 0,
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
