using System.IO;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Storage
{
    public class VMDriveStorageSettings
    {
        public VirtualCatletDriveType Type { get; set; }

        public int ControllerLocation { get; set; }
        public int ControllerNumber { get; set; }


        public static EitherAsync<Error, Seq<VMDriveStorageSettings>> PlanDriveStorageSettings(
            HostSettings hostSettings, CatletConfig config, VMStorageSettings storageSettings)
        {
            return config.VCatlet.Drives
                .ToSeq().MapToEitherAsync((index, c) =>
                    FromDriveConfig(hostSettings, storageSettings, c, index).ToEither()).ToAsync();
        }

        public static EitherAsync<Error, VMDriveStorageSettings> FromDriveConfig(
            HostSettings hostSettings, VMStorageSettings storageSettings, VirtualCatletDriveConfig driveConfig,
            int index)
        {
            const int
                controllerNumber = 0; //currently this will not be configurable, but keep it here at least as constant
            var controllerLocation =
                index; //later, when adding controller config support, we will have to add a logic to 
            //set location relative to the free slots for each controller                   


            //if it is not a vhd, we only need controller settings
            if (driveConfig.Type != VirtualCatletDriveType.VHD)
            {
                VMDriveStorageSettings result;
                if (driveConfig.Type == VirtualCatletDriveType.DVD)
                    result = new VMDvDStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = VirtualCatletDriveType.DVD,
                        Path = driveConfig.Template,
                    };
                else
                    result = new VMDriveStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = driveConfig.Type.GetValueOrDefault(VirtualCatletDriveType.PHD)
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
                (from resolvedPath in names.ResolveStorageBasePath(hostSettings.DefaultVirtualHardDiskPath)
                    from identifier in storageIdentifier.ToEither(
                        Error.New($"Unexpected missing storage identifier for disk '{driveConfig.Name}'."))
                        .ToAsync()
                    let planned = new HardDiskDriveStorageSettings
                    {
                        Type = driveConfig.Type.Value,
                        AttachPath = Path.Combine(Path.Combine(resolvedPath, identifier), $"{driveConfig.Name}.vhdx"),
                        DiskSettings = new DiskStorageSettings
                        {
                            StorageNames = names,
                            StorageIdentifier = storageIdentifier,
                            ParentSettings =
                                string.IsNullOrWhiteSpace(driveConfig.Template)
                                    ? Option<DiskStorageSettings>.None
                                    : DiskStorageSettings
                                        .FromTemplateString(hostSettings, driveConfig.Template),
                            Path = Path.Combine(resolvedPath, identifier),
                            FileName = $"{driveConfig.Name}.vhdx",
                            // ReSharper disable once StringLiteralTypo
                            Name = driveConfig.Name,
                            SizeBytes = driveConfig.Size.ToOption().Match(None: () => 1 * 1024L * 1024 * 1024,
                                Some: s => s * 1024L * 1024 * 1024)
                        },
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation
                    }
                    select planned as VMDriveStorageSettings);
        }
    }
}