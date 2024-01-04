using System.IO;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Storage
{
    public class VMDriveStorageSettings
    {
        public CatletDriveType Type { get; set; }

        public int ControllerLocation { get; set; }
        public int ControllerNumber { get; set; }


        public static EitherAsync<Error, Seq<VMDriveStorageSettings>> PlanDriveStorageSettings(
            VmHostAgentConfiguration vmHostAgentConfig, HostSettings hostSettings, CatletConfig config, VMStorageSettings storageSettings)
        {
            return config.Drives
                .ToSeq().MapToEitherAsync((index, c) =>
                    FromDriveConfig(vmHostAgentConfig, hostSettings, storageSettings, c, index).ToEither()).ToAsync();
        }

        public static EitherAsync<Error, VMDriveStorageSettings> FromDriveConfig(
            VmHostAgentConfiguration vmHostAgentConfig, HostSettings hostSettings, VMStorageSettings storageSettings, CatletDriveConfig driveConfig,
            int index)
        {
            const int
                controllerNumber = 0; //currently this will not be configurable, but keep it here at least as constant
            var controllerLocation =
                index; //later, when adding controller config support, we will have to add a logic to 
            //set location relative to the free slots for each controller                   


            //if it is not a vhd, we only need controller settings
            if (driveConfig.Type.HasValue && driveConfig.Type != CatletDriveType.VHD)
            {
                VMDriveStorageSettings result;
                if (driveConfig.Type == CatletDriveType.DVD)
                    result = new VMDvDStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = CatletDriveType.DVD,
                        Path = driveConfig.Source,
                    };
                else
                    result = new VMDriveStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = driveConfig.Type.GetValueOrDefault(CatletDriveType.PHD)
                    };

                return Prelude.RightAsync<Error, VMDriveStorageSettings>(result);
            }

            //so far for the simple part, now the complicated case - a vhd disk...

            var projectName = storageSettings.StorageNames.ProjectName;
            var environmentName = storageSettings.StorageNames.EnvironmentName;
            var dataStoreName = storageSettings.StorageNames.DataStoreName;
            var storageIdentifier = storageSettings.StorageIdentifier;

            var names = new StorageNames
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName
            };


            return
                (from resolvedPath in names.ResolveStorageBasePath(vmHostAgentConfig, hostSettings.DefaultVirtualHardDiskPath)
                    from identifier in storageIdentifier.ToEither(
                        Error.New($"Unexpected missing storage identifier for disk '{driveConfig.Name}'."))
                        .ToAsync()
                    let planned = new HardDiskDriveStorageSettings
                    {
                        Type = CatletDriveType.VHD,
                        AttachPath = Path.Combine(Path.Combine(resolvedPath, identifier), $"{driveConfig.Name}.vhdx"),
                        DiskSettings = new DiskStorageSettings
                        {
                            StorageNames = names,
                            StorageIdentifier = storageIdentifier,
                            ParentSettings =
                                string.IsNullOrWhiteSpace(driveConfig.Source)
                                    ? Option<DiskStorageSettings>.None
                                    : DiskStorageSettings
                                        .FromSourceString(vmHostAgentConfig, hostSettings, driveConfig.Source),
                            Path = Path.Combine(resolvedPath, identifier),
                            FileName = $"{driveConfig.Name}.vhdx",
                            // ReSharper disable once StringLiteralTypo
                            Name = driveConfig.Name,
                            SizeBytesCreate = driveConfig.Size.ToOption().Match(None: () => 1 * 1024L * 1024 * 1024,
                                Some: s => s * 1024L * 1024 * 1024),
                            SizeBytes = driveConfig.Size * 1024L * 1024 * 1024

                        },
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation
                    }
                    select planned as VMDriveStorageSettings);
        }
    }
}