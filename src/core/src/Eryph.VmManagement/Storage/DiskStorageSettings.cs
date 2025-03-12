using System;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage;

public class DiskStorageSettings
{
    public string Name { get; set; }

    public string Path { get; set; }
    
    public string FileName { get; set; }
    
    public int Generation { get; set; }
    
    public Guid DiskIdentifier { get; set; }

    public Option<DiskStorageSettings> ParentSettings { get; set; }

    /// <summary>
    /// The path to the parent of this disk. The parent path might be
    /// populated even if <see cref="ParentSettings"/> are <see cref="OptionNone"/>.
    /// This means that this disk is differential (i.e. it has parent) but
    /// the parent is missing.
    /// </summary>
    public Option<string> ParentPath { get; set; }

    public Option<string> StorageIdentifier { get; set; }
    
    public StorageNames StorageNames { get; set; }

    public long? UsedSizeBytes { get; set; }

    public long? SizeBytes { get; set; }
    
    public long? SizeBytesCreate { get; set; }

    /// <summary>
    /// Indicates that Hyper-V considers the disk to be usable,
    /// i.e. it has passed <c>Test-VHD</c>.
    /// </summary>
    public bool IsUsable { get; set; }

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
        from optionalVhdInfo in VhdQuery.GetVhdInfo(engine, path)
        from vhdInfo in optionalVhdInfo.ToEitherAsync(Error.New(
            $"Could not read VHD {path}"))
        from result in FromVhdInfo(engine, vmHostAgentConfig, vhdInfo)
        select result;

    private static EitherAsync<Error, DiskStorageSettings> FromVhdInfo(
        IPowershellEngine engine,
        VmHostAgentConfiguration vmHostAgentConfig,
        TypedPsObject<VhdInfo> vhdInfo) =>
        from isValid in VhdQuery.TestVhd(engine, vhdInfo.Value.Path)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let vhdPath = vhdInfo.Value.Path
        from result in GenePoolPaths.IsPathInGenePool(genePoolPath, vhdPath)
            ? FromGeneVhdInfo(genePoolPath, vhdInfo.Value, isValid)
            : from _ in RightAsync<Error, Unit>(unit)
            let nameAndId = StorageNames.FromVhdPath(vhdPath, vmHostAgentConfig)
            let parentPath = Optional(vhdInfo.Value.ParentPath).Filter(notEmpty)
            from parentVhdInfo in parentPath
                .Map(p => VhdQuery.GetVhdInfo(engine, p))
                .Sequence()
                .Map(o => o.Flatten())
            from parentSettings in parentVhdInfo
                .Map(i => FromVhdInfo(engine, vmHostAgentConfig, i))
                .Sequence()
            from name in DiskGenerationNames.GetFileNameWithoutSuffix(
                    vhdInfo.Value.Path, parentPath)
                .ToAsync()
            let generation = parentSettings.Map(p => p.Generation).IfNone(-1) + 1
            select new DiskStorageSettings
            {
                Path = System.IO.Path.GetDirectoryName(vhdInfo.Value.Path),
                Name = name,
                FileName = System.IO.Path.GetFileName(vhdInfo.Value.Path),
                StorageNames = nameAndId.Names,
                StorageIdentifier = nameAndId.StorageIdentifier,
                SizeBytes = vhdInfo.Value.Size,
                UsedSizeBytes = vhdInfo.Value.FileSize,
                DiskIdentifier = vhdInfo.Value.DiskIdentifier,
                Generation = generation,
                ParentSettings = parentSettings,
                ParentPath = parentPath,
                IsUsable = isValid && (parentPath.IsNone || parentSettings.IsSome),
            }
        select result;

    private static EitherAsync<Error, DiskStorageSettings> FromGeneVhdInfo(
        string genePoolPath,
        VhdInfo vhdInfo,
        bool isValid) =>
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
            IsUsable = isValid,
        };
}
