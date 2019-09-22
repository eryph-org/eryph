using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.VmHostAgent;
using Haipa.VmConfig;
using Haipa.VmManagement.Data;
using LanguageExt;
using LanguageExt.SomeHelp;
using LanguageExt.UnsafeValueAccess;
using Prelude = LanguageExt.Prelude;

namespace Haipa.VmManagement
{
    public static class Storage
    {
        public static Task<Either<PowershellFailure, Option<VMStorageSettings>>> DetectVMStorageSettings(
            Option<TypedPsObject<VirtualMachineInfo>> optionalVmInfo, HostSettings hostSettings,
            Func<string, Task> reportProgress)
        {
            return optionalVmInfo
                .MatchAsync(
                    Some: s =>
                    {
                        var namesAndId = PathToStorageNames(s.Value.Path, hostSettings.DefaultDataPath);

                        var settings =
                            (from resolvedPath in ResolveStorageBasePath(namesAndId.Names, hostSettings.DefaultDataPath)
                                from storageSettings in ComparePath(resolvedPath, s.Value.Path,
                                    namesAndId.StorageIdentifier)
                                select storageSettings);

                        return settings.Bind(e => e.Match(
                            Right: matchedPath => Prelude.RightAsync<PowershellFailure, VMStorageSettings>(
                                new VMStorageSettings
                                {
                                    StorageNames = namesAndId.Names,
                                    StorageIdentifier = namesAndId.StorageIdentifier,
                                    VMPath = matchedPath,
                                }).ToEither(),
                            Left: async (l) =>
                            {
                                //current behaviour is to soft fail by disabling storage changes
                                //however later we should add a option to strictly fail on all operations
                                await reportProgress(
                                    "Invalid machine storage settings. Storage management is disabled.");

                                return Prelude.Right<PowershellFailure, VMStorageSettings>(
                                    new VMStorageSettings
                                    {
                                        StorageNames = namesAndId.Names,
                                        StorageIdentifier = namesAndId.StorageIdentifier,
                                        VMPath = s.Value.Path,
                                        Frozen = true
                                    }
                                );

                            })).MapAsync(r => r.ToSome().ToOption());
                    },
                    None: () => Option<VMStorageSettings>.None);    

        }

        public static Task<Either<PowershellFailure, VMStorageSettings>> PlanVMStorageSettings(MachineConfig config, Option<VMStorageSettings> currentStorageSettings, HostSettings hostSettings, Func<Task<Either<PowershellFailure, string>>> idGeneratorFunc)
        {
            return ConfigToVMStorageSettings(config, hostSettings).BindAsync(newSettings =>
                currentStorageSettings.MatchAsync(
                    None: () => EnsureStorageId(newSettings, idGeneratorFunc),
                    Some: currentSettings => EnsureStorageId(newSettings, currentSettings, idGeneratorFunc)));

        }


        public static (StorageNames Names, Option<string> StorageIdentifier) PathToStorageNames(string path, string defaultPath)
        {
            var projectName = Prelude.Some("default");
            var environmentName = Prelude.Some("default");
            var dataStoreName = Option<string>.None;
            var dataStorePath = Option<string>.None;
            var storageIdentifier = Option<string>.None;

            if (path.StartsWith(defaultPath, StringComparison.InvariantCultureIgnoreCase))
            {
                dataStoreName = Prelude.Some("default");
                dataStorePath = defaultPath;
            }

            if (dataStorePath.IsSome)
            {
                var pathAfterDS = path.Remove(0, dataStorePath.ValueUnsafe().Length).TrimStart('\\');

                var pathRoot = Path.GetPathRoot(pathAfterDS);
                if (pathRoot.StartsWith("haipa_p"))
                {
                    projectName = pathRoot.Remove(0, "haipa_p".Length);
                }

                var idCandidate = pathAfterDS;

                if (idCandidate.Contains(Path.DirectorySeparatorChar))
                    idCandidate = Path.GetDirectoryName(pathAfterDS);

                if (!string.IsNullOrWhiteSpace(idCandidate) && pathRoot != idCandidate)
                    storageIdentifier = idCandidate;
            }

            var names = new StorageNames
            {
                DataStoreName = dataStoreName,
                ProjectName = projectName,
                EnvironmentName = environmentName
            };

            return (names, storageIdentifier);

        }

        public static async Task<Either<PowershellFailure, Seq<CurrentVMDiskStorageSettings>>> DetectDiskStorageSettings(
            IEnumerable<HardDiskDriveInfo> hdInfos, HostSettings hostSettings, IPowershellEngine engine)
        {
            var r = await hdInfos.Where(x=>!string.IsNullOrWhiteSpace(x.Path))
                .ToSeq().MapToEitherAsync(hdInfo =>
            {
                var res =
                    (
                    from vhdInfo in GetVhdInfo(hdInfo.Path, engine).ToAsync()
                    from snapshotInfo in GetSnapshotAndActualVhd(vhdInfo, engine).ToAsync()
                    let vhdPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.Path).IfNone(hdInfo.Path)
                    let parentPath = snapshotInfo.ActualVhd.Map(vhd => vhd.Value.ParentPath)
                    let snapshotPath = snapshotInfo.SnapshotVhd.Map(vhd => vhd.Value.Path)
                    let nameAndId = PathToStorageNames(Path.GetDirectoryName(vhdPath), hostSettings.DefaultVirtualHardDiskPath)
                    let diskSize = vhdInfo.Map(v => v.Value.Size).IfNone(0)
                    select
                        new CurrentVMDiskStorageSettings
                        {
                            Type = VirtualMachineDriveType.VHD,
                            Path = Path.GetDirectoryName(vhdPath),
                            Name = Path.GetFileNameWithoutExtension(vhdPath),
                            AttachPath = snapshotPath.IsSome? snapshotPath: vhdPath,
                            ParentPath = parentPath,
                            StorageNames = nameAndId.Names,
                            StorageIdentifier = nameAndId.StorageIdentifier,
                            SizeBytes = diskSize,
                            Frozen = snapshotPath.IsSome,
                            AttachedVMId = hdInfo.Id,
                            ControllerNumber = hdInfo.ControllerNumber,
                            ControllerLocation = hdInfo.ControllerLocation,
                        }).ToEither();
                return res;
            });

            return r;
        }

        public static Task<Either<PowershellFailure, Seq<VMDriveStorageSettings>>> PlanDriveStorageSettings(
            MachineConfig config, VMStorageSettings storageSettings, HostSettings hostSettings, IPowershellEngine engine)
        {
            return config.VM.Drives
                .ToSeq().MapToEitherAsync((index, c) =>
                    DriveConfigToDriveStorageSettings(index,c, storageSettings, hostSettings));

        }

        public static async Task<Either<PowershellFailure, Option<TypedPsObject<VhdInfo>>>> GetVhdInfo(string path, IPowershellEngine engine)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Option<TypedPsObject<VhdInfo>>.None;

            var res = await engine.GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("Get-VHD").AddArgument(path)).MapAsync(s => s.HeadOrNone());
            return res;
        }

        public static Task<Either<PowershellFailure, (Option<TypedPsObject<VhdInfo>> SnapshotVhd, Option<TypedPsObject<VhdInfo>> ActualVhd)>> GetSnapshotAndActualVhd(Option<TypedPsObject<VhdInfo>> vhdInfo, IPowershellEngine engine)
        {
            return vhdInfo.MapAsync(async (info) =>
            {

                var firstSnapshotVhdOption = Option<TypedPsObject<VhdInfo>>.None;
                var snapshotVhdOption = Option<TypedPsObject<VhdInfo>>.None;
                var actualVhdOption = Option<TypedPsObject<VhdInfo>>.None;

                // check for snapshots, return parent path if it is not a snapshot
                if (string.Equals(Path.GetExtension(info.Value.Path), ".avhdx", StringComparison.OrdinalIgnoreCase))
                {
                    snapshotVhdOption = vhdInfo;
                    firstSnapshotVhdOption = vhdInfo;
                }
                else
                {
                    actualVhdOption = vhdInfo;
                }

                while (actualVhdOption.IsNone)
                {

                    var eitherVhdInfo = await snapshotVhdOption.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot "})

                        .Map(snapshotVhd => string.IsNullOrWhiteSpace(snapshotVhd?.Value?.ParentPath)
                            ? Option<string>.None
                            : Option<string>.Some(snapshotVhd.Value.ParentPath))
                        .Bind(o => o.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot parent path"}))
                        .BindAsync(path => GetVhdInfo(path, engine))
                        .BindAsync(o => o.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot parent"}))
                        .ConfigureAwait(false);


                    if (eitherVhdInfo.IsLeft)
                        return Prelude
                            .Left<PowershellFailure, (Option<TypedPsObject<VhdInfo>> SnapshotVhd,
                                Option<TypedPsObject<VhdInfo>> ActualVhd)>(eitherVhdInfo.LeftAsEnumerable()
                                .FirstOrDefault());

                    info = eitherVhdInfo.IfLeft(new TypedPsObject<VhdInfo>(null));


                    if (string.Equals(Path.GetExtension(info.Value.Path), ".avhdx", StringComparison.OrdinalIgnoreCase))
                        snapshotVhdOption = info;
                    else
                        actualVhdOption = info;

                }

                return Prelude.Right((firstSnapshotVhdOption, actualVhdOption));

            }).IfNone(Prelude.Right((Option<TypedPsObject<VhdInfo>>.None, Option<TypedPsObject<VhdInfo>>.None)));


        }


        private static Task<Either<PowershellFailure, string>> ComparePath(string firstPath, string secondPath, Option<string> storageIdentifier)
        {
            return storageIdentifier.ToEither(new PowershellFailure {Message = "unknown VM storage identifier"})
                .BindAsync(id =>
                {
                    var fullPath = Path.Combine(firstPath, id);

                    if (!secondPath.Equals(fullPath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return Prelude
                            .LeftAsync<PowershellFailure, string>(new PowershellFailure { Message = "Path calculation failure" })
                            .ToEither();
                    }
                    return Prelude
                        .RightAsync<PowershellFailure, string>(firstPath)
                        .ToEither();

                });
                


        }

        private static Task<Either<PowershellFailure, VMDriveStorageSettings>> DriveConfigToDriveStorageSettings(int index,
            VirtualMachineDriveConfig driveConfig, VMStorageSettings storageSettings, HostSettings hostSettings)
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
                    result = new VMDVdStorageSettings
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
                (from resolvedPath in ResolveStorageBasePath(names, hostSettings.DefaultVirtualHardDiskPath).ToAsync()
                    from identifier in storageIdentifier.ToEither(new PowershellFailure
                            {Message = $"Unexpected missing storage identifier for disk '{driveConfig.Name}'."})
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
                        SizeBytes = driveConfig.Size.ToOption().Match( None: () => 1 * 1024L * 1024 * 1024,
                                                                      Some: s => s * 1024L * 1024 * 1024),
                        ControllerNumber = controllerNumber,
                        ControllerLocation = controllerLocation
                    }
                    select planned as VMDriveStorageSettings).ToEither();

        }


        private static Task<Either<PowershellFailure, VMStorageSettings>> ConfigToVMStorageSettings(MachineConfig config, HostSettings hostSettings)
        {
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

            if (!string.IsNullOrWhiteSpace(config.VM.Slug))
                storageIdentifier = Prelude.Some(config.VM.Slug);

            return ResolveStorageBasePath(names, hostSettings.DefaultDataPath).MapAsync(path => new VMStorageSettings
            {
                StorageNames = names,
                StorageIdentifier = storageIdentifier,
                VMPath = path
            });

        }

        private static Task<Either<PowershellFailure, VMStorageSettings>> EnsureStorageId(VMStorageSettings settings, Func<Task<Either<PowershellFailure, string>>> idGeneratorFunc)
        {
            return EnsureStorageId(settings, new VMStorageSettings(), idGeneratorFunc);
        }

        private static Task<Either<PowershellFailure, VMStorageSettings>> EnsureStorageId(VMStorageSettings first, VMStorageSettings second, Func<Task<Either<PowershellFailure, string>>> idGeneratorFunc)
        {
            if (second.Frozen)
                return Prelude.RightAsync<PowershellFailure, VMStorageSettings>(second).ToEither();


            return first.StorageIdentifier.MatchAsync(
                None:
                    () => second.StorageIdentifier.MatchAsync(
                    None: idGeneratorFunc,
                    Some: s => Prelude.RightAsync<PowershellFailure, string>(s).ToEither()),
                Some: s => Prelude.RightAsync<PowershellFailure, string>(s).ToEither()
                ).MapAsync(storageIdentifier =>

                    new VMStorageSettings
                    {
                        StorageNames = first.StorageNames,
                        StorageIdentifier = storageIdentifier,
                        VMPath = first.VMPath
                    });

        }

        private static Task<Either<PowershellFailure, string>> ResolveStorageBasePath(StorageNames names,string defaultPath)
        {
            return (
                      from dsName in names.DataStoreName.ToEitherAsync(new PowershellFailure { Message = "Unknown data store name. Cannot resolve path" })
                      from projectName in names.ProjectName.ToEitherAsync(new PowershellFailure { Message = "Unknown project name. Cannot resolve path" })
                      from environmentName in names.EnvironmentName.ToEitherAsync(new PowershellFailure { Message = "Unknown environment name. Cannot resolve path" })
                      from dsPath in LookupVMDatastorePathInEnvironment(dsName, environmentName, defaultPath).ToAsync()
                      from projectPath in JoinPathAndProject(dsPath, projectName).ToAsync()
                      select projectPath

                   ).ToEither();
        }

        private static Task<Either<PowershellFailure, string>> JoinPathAndProject(string dsPath, string projectName)
        {
            var result = dsPath;

            if (projectName != "default")
                result = Path.Combine(dsPath, $"haipa_p{projectName}");

            return Prelude.RightAsync<PowershellFailure, string>(result).ToEither();
        }

        static async Task<Either<PowershellFailure, string>> LookupVMDatastorePathInEnvironment(string datastore, string environment, string defaultPath)
        {
            return defaultPath;
        }

    }
}
