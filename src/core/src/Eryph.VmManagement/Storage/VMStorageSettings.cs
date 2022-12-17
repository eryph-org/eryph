using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
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
        public string DefaultVhdPath { get; set; }

        public bool Frozen { get; set; }

        public static EitherAsync<Error, Option<VMStorageSettings>> FromVM(HostSettings hostSettings,
            TypedPsObject<VirtualMachineInfo> vm)
        {

            var (names, storageIdentifier) = StorageNames.FromPath(vm.Value.Path, hostSettings.DefaultDataPath);

            async Task<VMStorageSettings> FromVMAsync()
            {
                return await
                    (from resolvedPath in names.ResolveStorageBasePath(hostSettings.DefaultDataPath)
                        from importVhdPath in names.ResolveStorageBasePath(hostSettings.DefaultVirtualHardDiskPath)
                        from storageSettings in ComparePath(resolvedPath, vm.Value.Path,
                            storageIdentifier)

                        select new VMStorageSettings
                        {
                            StorageNames = names,
                            StorageIdentifier = storageIdentifier,
                            VMPath = vm.Value.Path,
                            DefaultVhdPath = importVhdPath,
                        }).IfLeft(_ => 
                        new VMStorageSettings
                    {
                        StorageNames = names,
                        StorageIdentifier = storageIdentifier,
                        VMPath = vm.Value.Path,
                        DefaultVhdPath = vm.Value.Path,
                        Frozen = true
                    });

            }

            return Prelude.RightAsync<Error, VMStorageSettings>(FromVMAsync())
                .Map(r => r.ToSome().ToOption());
        }


        public static EitherAsync<Error, VMStorageSettings> FromCatletConfig(CatletConfig config,
            HostSettings hostSettings)
        {
            var projectName = Prelude.Some(string.IsNullOrWhiteSpace(config.Project) 
                ? "default": config.Project);
            var environmentName = Prelude.Some("default");
            var dataStoreName = Prelude.Some("default");
            var storageIdentifier = Option<string>.None;

            var names = new StorageNames
            {
                ProjectName = projectName,
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName
            };

            if (!string.IsNullOrWhiteSpace(config.VCatlet.Slug))
                storageIdentifier = Prelude.Some(config.VCatlet.Slug);

            return from dataPath in  names.ResolveStorageBasePath(hostSettings.DefaultDataPath)
                   from importVhdPath in names.ResolveStorageBasePath(hostSettings.DefaultVirtualHardDiskPath)
                   select new VMStorageSettings
                {
                    StorageNames = names,
                    StorageIdentifier = storageIdentifier,
                    VMPath = dataPath,
                    DefaultVhdPath = importVhdPath 
                };
        }


        public static EitherAsync<Error, VMStorageSettings> Plan(
            HostSettings hostSettings,
            string newStorageId,
            CatletConfig config,
            Option<VMStorageSettings> currentStorageSettings)
        {
            return FromCatletConfig(config, hostSettings).Bind(newSettings =>
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
                    VMPath = first.VMPath,
                    DefaultVhdPath = first.DefaultVhdPath
                });
        }
    }
}