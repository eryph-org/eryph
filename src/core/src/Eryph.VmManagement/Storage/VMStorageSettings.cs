using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel.Machine;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.SomeHelp;

namespace Eryph.VmManagement.Storage
{
    public class VMStorageSettings
    {
        public StorageNames StorageNames { get; set; }
        public Option<string> StorageIdentifier { get; set; }

        public string VMPath { get; set; }
        public bool Frozen { get; set; }

        public static EitherAsync<Error, Option<VMStorageSettings>> FromVM(HostSettings hostSettings,
            TypedPsObject<VirtualMachineInfo> vm)
        {

            var (names, storageIdentifier) = StorageNames.FromPath(vm.Value.Path, hostSettings.DefaultDataPath);

            async Task<VMStorageSettings> FromVMAsync()
            {
                return await
                    (from resolvedPath in names.ResolveStorageBasePath(hostSettings.DefaultDataPath)
                        from storageSettings in ComparePath(resolvedPath, vm.Value.Path,
                            storageIdentifier)

                        select new VMStorageSettings
                        {
                            StorageNames = names,
                            StorageIdentifier = storageIdentifier,
                            VMPath = vm.Value.Path
                        }).IfLeft(_ => 
                        new VMStorageSettings
                    {
                        StorageNames = names,
                        StorageIdentifier = storageIdentifier,
                        VMPath = vm.Value.Path,
                        Frozen = true
                    });

            }

            return Prelude.RightAsync<Error, VMStorageSettings>(FromVMAsync())
                .Map(r => r.ToSome().ToOption());
        }


        public static EitherAsync<Error, VMStorageSettings> FromMachineConfig(MachineConfig config,
            HostSettings hostSettings)
        {
            var projectName = Prelude.Some("default");
            var environmentName = Prelude.Some("default");
            var dataStoreName = Prelude.Some("default");
            var storageIdentifier = Option<string>.None;

            var names = new StorageNames
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName
            };

            if (!string.IsNullOrWhiteSpace(config.VM.Slug))
                storageIdentifier = Prelude.Some(config.VM.Slug);

            return names.ResolveStorageBasePath(hostSettings.DefaultDataPath).Map(
                path => new VMStorageSettings
            {
                StorageNames = names,
                StorageIdentifier = storageIdentifier,
                VMPath = path
            });
        }


        public static EitherAsync<Error, VMStorageSettings> Plan(
            HostSettings hostSettings,
            string newStorageId,
            MachineConfig config,
            Option<VMStorageSettings> currentStorageSettings)
        {
            return FromMachineConfig(config, hostSettings).Bind(newSettings =>
                currentStorageSettings.Match(
                    None: () => EnsureStorageId(newStorageId, newSettings).ToError().ToAsync(),
                    Some: currentSettings => EnsureStorageId(newStorageId, newSettings, currentSettings)
                        .ToError().ToAsync()));
        }

        private static EitherAsync<Error, string> ComparePath(string firstPath, string secondPath,
            Option<string> storageIdentifier)
        {
            return storageIdentifier.ToEither(Error.New("unknown VM storage identifier"))
                .ToAsync()
                .Bind(id =>
                {
                    var fullPath = Path.Combine(firstPath, id);

                    if (!secondPath.Equals(fullPath, StringComparison.InvariantCultureIgnoreCase))
                        return Prelude
                            .LeftAsync<Error, string>(Error.New(
                                "Path calculation failure"));

                    return Prelude
                        .RightAsync<Error, string>(firstPath);
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