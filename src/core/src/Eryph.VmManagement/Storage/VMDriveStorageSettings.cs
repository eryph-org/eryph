using System.IO;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage
{
    public class VMDriveStorageSettings
    {
        public CatletDriveType Type { get; set; }

        public int ControllerLocation { get; set; }
        public int ControllerNumber { get; set; }


        public static EitherAsync<Error, Seq<VMDriveStorageSettings>> PlanDriveStorageSettings(
            VmHostAgentConfiguration vmHostAgentConfig,
            CatletConfig config,
            VMStorageSettings storageSettings,
            IPowershellEngine powershellEngine)
        {
            return config.Drives
                .ToSeq().MapToEitherAsync((index, c) =>
                    FromDriveConfig(vmHostAgentConfig, storageSettings, c, powershellEngine, index).ToEither()).ToAsync();
        }

        public static EitherAsync<Error, VMDriveStorageSettings> FromDriveConfig(
            VmHostAgentConfiguration vmHostAgentConfig,
            VMStorageSettings storageSettings,
            CatletDriveConfig driveConfig,
            IPowershellEngine powershellEngine,
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
            var dataStoreName = Prelude.Optional(driveConfig.Store).Filter(Prelude.notEmpty).IfNone("default");
            var storageIdentifier = Prelude.Optional(driveConfig.Location).Filter(Prelude.notEmpty).Match(
                Some: s => s,
                None: storageSettings.StorageIdentifier);

            var names = new StorageNames
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName
            };


            return
                (from resolvedPath in names.ResolveVolumeStorageBasePath(vmHostAgentConfig)
                    from identifier in storageIdentifier.ToEitherAsync(
                        Error.New($"Unexpected missing storage identifier for disk '{driveConfig.Name}'."))
                    let fileName = $"{driveConfig.Name}.vhdx"
                    let attachPath = Path.Combine(resolvedPath, identifier, fileName)
                    from psVhdInfo in VhdQuery.GetVhdInfo(powershellEngine, attachPath).ToError().ToAsync()
                    let vhdInfo = psVhdInfo.Map(ps => ps.Value)
                    from parentOptions in match(
                        Optional(driveConfig.Source).Filter(notEmpty),
                        Some: src => 
                            from dss in DiskStorageSettings.FromSourceString(vmHostAgentConfig, src)
                                .ToEitherAsync(Error.New("The catlet drive source is invalid"))
                            select Some(dss),
                        None: () => RightAsync<Error, Option<DiskStorageSettings>>(None))
                    from parentVhdInfo in match(
                        parentOptions,
                        Some: po =>
                            from ovi in VhdQuery.GetVhdInfo(powershellEngine, Path.Combine(po.Path, po.FileName)).ToError().ToAsync()
                            from vi in ovi.ToEitherAsync(Error.New("The catlet drive source does not exist"))
                            select Some(vi.Value),
                        None: () => RightAsync<Error, Option<VhdInfo>>(None))
                    let minimumSize = vhdInfo.Map(i => i.MinimumSize ?? i.Size) | parentVhdInfo.Map(i => i.MinimumSize ?? i.Size)
                    let configuredSize = Optional(driveConfig.Size).Filter(notDefault).Map(s => s * 1024L * 1024 * 1024)
                    from _ in guard(configuredSize.IsNone || minimumSize.IsNone || configuredSize >= minimumSize,
                        Error.New("Disk size is below minimum size of the virtual disk"))
                    let planned = new HardDiskDriveStorageSettings
                    {
                        Type = CatletDriveType.VHD,
                        AttachPath = attachPath,
                        DiskSettings = new DiskStorageSettings
                        {
                            StorageNames = names,
                            StorageIdentifier = storageIdentifier,
                            ParentSettings = parentOptions,
                            Path = Path.Combine(resolvedPath, identifier),
                            // ReSharper disable once StringLiteralTypo
                            FileName = fileName,
                            Name = driveConfig.Name,
                            SizeBytesCreate = (configuredSize | parentVhdInfo.Map(i => i.Size)).IfNone(1 * 1024L * 1024 * 1024),
                            SizeBytes = configuredSize.IsSome ? configuredSize.ValueUnsafe() : null,
                        },
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation
                    }
                    select planned as VMDriveStorageSettings);
        }
    }
}