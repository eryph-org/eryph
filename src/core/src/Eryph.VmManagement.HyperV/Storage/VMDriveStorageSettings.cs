using System;
using System.IO;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage;

public class VMDriveStorageSettings
{
    public CatletDriveType Type { get; set; }

    public int ControllerLocation { get; set; }

    public int ControllerNumber { get; set; }

    public static EitherAsync<Error, Seq<VMDriveStorageSettings>> PlanDriveStorageSettings(
        VmHostAgentConfiguration vmHostAgentConfig,
        CatletConfig config,
        VMStorageSettings storageSettings,
        Func<string, EitherAsync<Error, Option<VhdInfo>>> getVhdInfo,
        Func<string, EitherAsync<Error, bool>> testVhd,
        Seq<UniqueGeneIdentifier> resolvedGenes)
    {
        return config.Drives.ToSeq()
            .Map((index, c) => FromDriveConfig(vmHostAgentConfig, storageSettings, c, getVhdInfo, testVhd, resolvedGenes, index))
            .ToSeq().SequenceSerial();
    }

    private static EitherAsync<Error, VMDriveStorageSettings> FromDriveConfig(
        VmHostAgentConfiguration vmHostAgentConfig,
        VMStorageSettings storageSettings,
        CatletDriveConfig driveConfig,
        Func<string, EitherAsync<Error, Option<VhdInfo>>> getVhdInfo,
        Func<string, EitherAsync<Error, bool>> testVhd,
        Seq<UniqueGeneIdentifier> resolvedGenes,
        int index)
    {
        //currently this will not be configurable, but keep it here at least as constant
        const int controllerNumber = 0;

        //later, when adding controller config support, we will have to add a logic to 
        //set location relative to the free slots for each controller   
        var controllerLocation = index;

        return Optional(driveConfig.Type).IfNone(CatletDriveType.Vhd) switch
        {
            CatletDriveType.Dvd => new VMDvDStorageSettings
            {
                ControllerNumber = controllerNumber,
                ControllerLocation = controllerLocation,
                Type = CatletDriveType.Dvd,
                Path = driveConfig.Source,
            },
            CatletDriveType.Vhd or CatletDriveType.SharedVhd or CatletDriveType.VhdSet =>
                FromVhdDriveConfig(vmHostAgentConfig, storageSettings, driveConfig,
                    getVhdInfo, testVhd, resolvedGenes, controllerNumber, controllerLocation),
            _ => LeftAsync<Error, VMDriveStorageSettings>(Error.New(
                $"The drive type {driveConfig.Type} is not supported")),
        };
    }

    private static EitherAsync<Error, VMDriveStorageSettings> FromVhdDriveConfig(
        VmHostAgentConfiguration vmHostAgentConfig,
        VMStorageSettings storageSettings,
        CatletDriveConfig driveConfig,
        Func<string, EitherAsync<Error, Option<VhdInfo>>> getVhdInfo,
        Func<string, EitherAsync<Error, bool>> testVhd,
        Seq<UniqueGeneIdentifier> resolvedGenes,
        int controllerNumber,
        int controllerLocation) =>
        from dataStoreName in DataStoreName.NewEither(driveConfig.Store)
            .MapLeft(e => Error.New($"The configured data store of the catlet drive '{driveConfig.Name}' is invalid.", e))
            .ToAsync()
        let driveStorageNames = new StorageNames
        {
            ProjectName = storageSettings.StorageNames.ProjectName,
            EnvironmentName = storageSettings.StorageNames.EnvironmentName,
            DataStoreName = dataStoreName.Value,
        }
        let storageIdentifier = Optional(driveConfig.Location).Filter(notEmpty).Match(
            Some: s => s,
            None: storageSettings.StorageIdentifier)
        from resolvedPath in driveStorageNames.ResolveVolumeStorageBasePath(vmHostAgentConfig)
        from parentOptions in Optional(driveConfig.Source)
            .Filter(notEmpty)
            .Map(src => from dss in DiskStorageSettings.FromSourceString(vmHostAgentConfig, resolvedGenes, src)
                            .ToEitherAsync(Error.New($"The catlet drive source '{driveConfig.Source}' is invalid."))
                        select dss)
            .Sequence()
        let parentPath = parentOptions.Map(p => Path.Combine(p.Path, p.FileName))
        let generation = parentOptions.Map(p => p.Generation + 1).IfNone(0)
        from identifier in storageIdentifier.ToEitherAsync(
            Error.New($"Unexpected missing storage identifier for disk '{driveConfig.Name}'."))
        let fileName = driveConfig.Type switch
        {
            CatletDriveType.SharedVhd => $"{driveConfig.Name}.vhdx",
            CatletDriveType.VhdSet => $"{driveConfig.Name}.vhds",
            _ => $"{driveConfig.Name}.vhdx",
        }
        from attachPath in driveConfig.Type is CatletDriveType.SharedVhd or CatletDriveType.VhdSet
            ? Path.Combine(resolvedPath, identifier, fileName)
            : DiskGenerationNames.AddGenerationSuffix(
                    Path.Combine(resolvedPath, identifier, fileName), generation)
                .ToAsync()
        let configuredSize = Optional(driveConfig.Size).Filter(notDefault).Map(s => s * 1024L * 1024 * 1024)
        from vhdInfo in getVhdInfo(attachPath)
        let vhdMinimumSize = vhdInfo.Bind(i => Optional(i.MinimumSize))
        let vhdSize = vhdInfo.Map(i => i.Size)
        from parentVhdInfo in parentPath
            .Map(p => from ovi in getVhdInfo(p)
                      from vi in ovi.ToEitherAsync(Error.New($"The catlet drive source '{driveConfig.Source}' does not exist."))
                      select vi)
            .Sequence()
        from _2 in parentPath
            .Map(p => from isUsable in testVhd(p)
                      from _  in guard(isUsable,
                          Error.New($"The catlet drive source '{driveConfig.Source}' is considered unusable by Hyper-V."))
                      select unit)
            .Sequence()
        let parentVhdMinimumSize = parentVhdInfo.Bind(i => Optional(i.MinimumSize))
        let parentVhdSize = parentVhdInfo.Map(i => i.Size)
        // The MinimumSize of a disk can be null. This seems to happen when the disk
        // does not have a partition table. It this case, we use the current size as
        // the minimum size.
        let minimumSize = vhdMinimumSize | vhdSize | parentVhdMinimumSize | parentVhdSize
        from _3 in guard(configuredSize.IsNone || minimumSize.IsNone || configuredSize >= minimumSize,
            Error.New("Disk size is below minimum size of the virtual disk"))
        let planned = new HardDiskDriveStorageSettings
        {
            Type = driveConfig.Type.GetValueOrDefault(),
            AttachPath = attachPath,
            DiskSettings = new DiskStorageSettings
            {
                StorageNames = driveStorageNames,
                StorageIdentifier = storageIdentifier,
                ParentSettings = parentOptions,
                ParentPath = parentPath,
                Path = Path.Combine(resolvedPath, identifier),
                FileName = fileName,
                Name = driveConfig.Name,
                SizeBytesCreate = (configuredSize | parentVhdInfo.Map(i => i.Size))
                    .IfNone(1 * 1024L * 1024 * 1024),
                SizeBytes = configuredSize.Map(s => (long?)s).IfNoneUnsafe((long?)null),
                Generation = generation,
                IsUsable = true,
            },
            ControllerNumber = controllerNumber,
            ControllerLocation = controllerLocation
        }
        select (VMDriveStorageSettings)planned;
}
