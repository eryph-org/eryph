using System;
using System.Collections.Generic;
using System.IO;
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



        public static Task<Either<PowershellFailure, Option<VMStorageSettings>>> DetectVMStorageSettings(Option<TypedPsObject<VirtualMachineInfo>> optionalVmInfo, HostSettings hostSettings, Func<string, Task> reportProgress)
        {
            return optionalVmInfo
                    .MatchAsync(
                        Some: s => from namesAndId in PathToStorageNames(s.Value.Path, hostSettings.DefaultDataPath)
                            from resolvedPath in ResolveStorageBasePath(namesAndId.Names, hostSettings.DefaultDataPath)
                            from storageSettings in ComparePath(resolvedPath, s.Value.Path,
                                    namesAndId.StorageIdentifier)
                                .Apply(e => e.Match(
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
                                    }))

                            select storageSettings.Apply(r => r.ToSome().ToOption()),
                        None: () =>
                            Option<VMStorageSettings>.None);
        }

        public static Task<Either<PowershellFailure, VMStorageSettings>> PlanVMStorageSettings(MachineConfig config, Option<VMStorageSettings> currentStorageSettings, HostSettings hostSettings, Func<Task<Either<PowershellFailure, string>>> idGeneratorFunc)
        {
            return ConfigToVMStorageSettings(config, hostSettings).BindAsync(newSettings =>
                currentStorageSettings.MatchAsync(
                    None: () => EnsureStorageId(newSettings, idGeneratorFunc),
                    Some: currentSettings => EnsureStorageId(newSettings, currentSettings, idGeneratorFunc)));

        }


        public static Task<Either<PowershellFailure, (StorageNames Names, Option<string> StorageIdentifier)>> PathToStorageNames(string path, string defaultPath)
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

            return Prelude.RightAsync<PowershellFailure, (StorageNames Names, Option<string> StorageIdentifier)>((names, storageIdentifier)).ToEither();

        }

        public static Task<Either<PowershellFailure, Seq<VMDiskStorageSettings>>> DetectDiskStorageSettings(
            IEnumerable<HardDiskDriveInfo> hdInfos, HostSettings hostSettings, IPowershellEngine engine)
        {
            return hdInfos.ToSeq().MapToEitherAsync(hdInfo => 

                    from nameAndId in PathToStorageNames(Path.GetDirectoryName(hdInfo.Path), hostSettings.DefaultVirtualHardDiskPath)
                    from parentPath in GetParentPath(hdInfo.Path, engine)
                    select (hdInfo, nameAndId.Names, nameAndId.StorageIdentifier, parentPath))    
                    
                .MapAsync(seq=> seq.Map(t =>
                {
                    var (hardDiskDriveInfo, storageNames, option, parentPath) = t;
                    return new VMDiskStorageSettings
                    {
                        Path = Path.GetDirectoryName(hardDiskDriveInfo.Path),
                        Name = Path.GetFileName(hardDiskDriveInfo.Path),
                        ParentPath = parentPath,
                        StorageNames = storageNames,
                        StorageIdentifier = option,
                    };
                }).ToSeq());

        }

        public static Task<Either<PowershellFailure, Seq<VMDiskStorageSettings>>> PlanDiskStorageSettings(
            MachineConfig config, VMStorageSettings storageSettings, Seq<VMDiskStorageSettings> currentDiskStorageSettings, HostSettings hostSettings)
        {
            if (storageSettings.Frozen)
                return Prelude.RightAsync<PowershellFailure, Seq<VMDiskStorageSettings>>(currentDiskStorageSettings).ToEither();
            
            return config.VM.Disks.ToSeq().MapToEitherAsync(c => DiskConfigToDiskStorageSettings(c, storageSettings, hostSettings));
        }

        private static async Task<Either<PowershellFailure, Option<string>>> GetParentPath(string path, IPowershellEngine engine)
        {
            var res = await engine.GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("Get-VHD").AddArgument(path))
                .MapAsync(s => s.HeadOrNone().MapAsync(h => h.Value.ParentPath).ToOption());
            return res;
        }


        private static Task<Either<PowershellFailure, string>> ComparePath(string firstPath, string secondPath, Option<string> storageIdentifier)
        {
            return storageIdentifier.ToEither(new PowershellFailure {Message = "unknown VM storage identifier"})
                .BindAsync(id =>
                {
                    var fullPath = Path.Combine(secondPath, id);

                    if (!firstPath.Equals(fullPath, StringComparison.InvariantCultureIgnoreCase))
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

        private static Task<Either<PowershellFailure, VMDiskStorageSettings>> DiskConfigToDiskStorageSettings(VirtualMachineDiskConfig diskConfig, VMStorageSettings storageSettings, HostSettings hostSettings)
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


            if (storageIdentifier.IsNone)
                storageIdentifier = storageSettings.StorageIdentifier;

            return                                
                from resolvedPath in ResolveStorageBasePath(names, hostSettings.DefaultVirtualHardDiskPath)
                from identifier in storageIdentifier.ToEither(new PowershellFailure{ Message = $"Unexpected missing storage identifier for disk '{diskConfig.Name}'."}).ToAsync().ToEither()
                select new VMDiskStorageSettings
                {
                    StorageNames = names,
                    StorageIdentifier = storageIdentifier,
                    ParentPath = diskConfig.Template,
                    Path = Path.Combine(resolvedPath,identifier),
                    // ReSharper disable once StringLiteralTypo
                    Name = $"{diskConfig.Name}.vhdx"
                };

        }


        private static Task<Either<PowershellFailure, VMStorageSettings>> ConfigToVMStorageSettings(MachineConfig config, HostSettings hostSettings)
        {
            var projectName = Prelude.Some("default");
            var environmentName = Prelude.Some("default");
            var dataStoreName = Prelude.Some("default");
            var storageIdentifier = Prelude.None;

            var names = new StorageNames()
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName,

            };

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
