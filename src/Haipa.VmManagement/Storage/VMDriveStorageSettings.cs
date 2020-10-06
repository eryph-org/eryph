using System.IO;
using System.Threading.Tasks;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.VmManagement.Storage
{
    public class VMDriveStorageSettings
    {
        public VirtualMachineDriveType Type { get; set; }

        public int ControllerLocation { get; set; }
        public int ControllerNumber { get; set; }

        
        public static Task<Either<PowershellFailure, Seq<VMDriveStorageSettings>>> PlanDriveStorageSettings(
            HostSettings hostSettings, MachineConfig config, VMStorageSettings storageSettings)
        {
            return config.VM.Drives
                .ToSeq().MapToEitherAsync((index, c) =>
                    FromDriveConfig(hostSettings, storageSettings, c, index));

        }

        public static Task<Either<PowershellFailure, VMDriveStorageSettings>> FromDriveConfig(
            HostSettings hostSettings, VMStorageSettings storageSettings, VirtualMachineDriveConfig driveConfig, int index)
        {

            const int controllerNumber = 0;  //currently this will not be configurable, but keep it here at least as constant
            var controllerLocation = index;  //later, when adding controller config support, we will have to add a logic to 
            //set location relative to the free slots for each controller                   


            //if it is not a vhd, we only need controller settings
            if (driveConfig.Type != VirtualMachineDriveType.VHD)
            {
                VMDriveStorageSettings result;
                if (driveConfig.Type == VirtualMachineDriveType.DVD)
                {
                    result = new VMDvDStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = VirtualMachineDriveType.DVD
                    };
                }
                else
                {
                    result = new VMDriveStorageSettings
                    {
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation,
                        Type = driveConfig.Type.GetValueOrDefault(VirtualMachineDriveType.PHD)
                    };
                }

                return Prelude.RightAsync<PowershellFailure, VMDriveStorageSettings>(result).ToEither();
            }

            //so far for the simple part, now the complicated case - a vhd disk...

            var projectName = Prelude.Some("default");
            var environmentName = Prelude.Some("default");
            var dataStoreName = Prelude.Some("default");
            var storageIdentifier = Option<string>.None;

            var names = new StorageNames()
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName,

            };



            if (storageIdentifier.IsNone)
                storageIdentifier = storageSettings.StorageIdentifier;


            return
                (from resolvedPath in names.ResolveStorageBasePath(hostSettings.DefaultVirtualHardDiskPath).ToAsync()
                 from identifier in storageIdentifier.ToEither(new PowershellFailure
                 { Message = $"Unexpected missing storage identifier for disk '{driveConfig.Name}'." })
                     .ToAsync()
                     .ToEither().ToAsync()

                 let planned = new VMDiskStorageSettings
                 {
                     Type = driveConfig.Type.Value,
                     StorageNames = names,
                     StorageIdentifier = storageIdentifier,
                     ParentPath = driveConfig.Template,
                     Path = Path.Combine(resolvedPath, identifier),
                     AttachPath = Path.Combine(Path.Combine(resolvedPath, identifier), $"{driveConfig.Name}.vhdx"),
                     // ReSharper disable once StringLiteralTypo
                     Name = driveConfig.Name,
                     SizeBytes = driveConfig.Size.ToOption().Match(None: () => 1 * 1024L * 1024 * 1024,
                                                                   Some: s => s * 1024L * 1024 * 1024),
                     ControllerNumber = controllerNumber,
                     ControllerLocation = controllerLocation
                 }
                 select planned as VMDriveStorageSettings).ToEither();

        }
    }
}