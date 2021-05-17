using System;
using System.IO;
using System.Threading.Tasks;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines.Config;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.SomeHelp;

namespace Haipa.VmManagement.Storage
{
    public class VMStorageSettings
    {
        public StorageNames StorageNames { get; set; }
        public Option<string> StorageIdentifier { get; set; }

        public string VMPath { get; set; }
        public bool Frozen { get; set; }

        public static Task<Either<PowershellFailure, Option<VMStorageSettings>>> FromVM(HostSettings hostSettings, TypedPsObject<VirtualMachineInfo> vm)
        {
            var (names, storageIdentifier) = StorageNames.FromPath(vm.Value.Path, hostSettings.DefaultDataPath);

            var settings =
                (from resolvedPath in names.ResolveStorageBasePath(hostSettings.DefaultDataPath)
                    from storageSettings in ComparePath(resolvedPath, vm.Value.Path,
                        storageIdentifier)
                    select storageSettings);

            return settings.Bind(e => e.Match(
                Right: matchedPath => Prelude.RightAsync<PowershellFailure, VMStorageSettings>(
                    new VMStorageSettings
                    {
                        StorageNames = names,
                        StorageIdentifier = storageIdentifier,
                        VMPath = matchedPath,
                    }).ToEither(),
                Left: (l) => Prelude.RightAsync<PowershellFailure, VMStorageSettings>(
                    new VMStorageSettings
                    {
                        StorageNames = names,
                        StorageIdentifier = storageIdentifier,
                        VMPath = vm.Value.Path,
                        Frozen = true
                    }
                ).ToEither()
                )).MapAsync(r => r.ToSome().ToOption());
        }


        public static Task<Either<PowershellFailure, VMStorageSettings>> FromMachineConfig(MachineConfig config, HostSettings hostSettings)
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

            return names.ResolveStorageBasePath(hostSettings.DefaultDataPath).MapAsync(path => new VMStorageSettings
            {
                StorageNames = names,
                StorageIdentifier = storageIdentifier,
                VMPath = path
            });

        }


        public static Task<Either<PowershellFailure, VMStorageSettings>> Plan(
            HostSettings hostSettings,
            string newStorageId,
            MachineConfig config, 
            Option<VMStorageSettings> currentStorageSettings)
        {
            return FromMachineConfig(config, hostSettings).BindAsync(newSettings =>
                currentStorageSettings.MatchAsync(
                    None: () => EnsureStorageId(newStorageId, newSettings),
                    Some: currentSettings => EnsureStorageId(newStorageId, newSettings, currentSettings)));

        }

        private static Task<Either<PowershellFailure, string>> ComparePath(string firstPath, string secondPath, Option<string> storageIdentifier)
        {
            return storageIdentifier.ToEither(new PowershellFailure { Message = "unknown VM storage identifier" })
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

        private static Task<Either<PowershellFailure, VMStorageSettings>> EnsureStorageId(
            string newStorageId, VMStorageSettings settings)
        {
            return EnsureStorageId(newStorageId, settings, new VMStorageSettings());
        }

        private static Task<Either<PowershellFailure, VMStorageSettings>> EnsureStorageId(
            string newStorageId,
            VMStorageSettings first, VMStorageSettings second)
        {
            if (second.Frozen)
                return Prelude.RightAsync<PowershellFailure, VMStorageSettings>(second).ToEither();


            return first.StorageIdentifier.MatchAsync(
                None:
                () => second.StorageIdentifier.MatchAsync(
                    None: () => newStorageId,
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


    }
}