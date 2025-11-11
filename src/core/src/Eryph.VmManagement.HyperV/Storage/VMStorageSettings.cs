using System;
using System.IO;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

#nullable enable

namespace Eryph.VmManagement.Storage
{
    public readonly struct VMStorageSettings
    {
        public StorageNames StorageNames { get; init; }

        public Option<string> StorageIdentifier { get; init; }

        public string VMPath { get; init; }
        
        public string DefaultVhdPath { get; init; }

        public bool Frozen { get; init; }

        public static EitherAsync<Error, VMStorageSettings> FromVm(
            VmHostAgentConfiguration vmHostAgentConfig,
            TypedPsObject<VirtualMachineInfo> vm) =>
            from _ in RightAsync<Error, Unit>(unit)
            let storageNames = StorageNames.FromVmPath(vm.Value.Path, vmHostAgentConfig)
            from settings in FromStorageNames(storageNames.Names, storageNames.StorageIdentifier, vm.Value.Path, vmHostAgentConfig)
                .BiBind(
                    Right: RightAsync<Error, VMStorageSettings>,
                    Left: _ => RightAsync<Error, VMStorageSettings>(new VMStorageSettings
                    {
                        StorageNames = storageNames.Names,
                        StorageIdentifier = storageNames.StorageIdentifier,
                        VMPath = vm.Value.Path,
                        DefaultVhdPath = vm.Value.Path,
                        Frozen = true
                    }))
            select settings;

        private static EitherAsync<Error, VMStorageSettings> FromStorageNames(
            StorageNames names,
            Option<string> storageIdentifier,
            string vmPath,
            VmHostAgentConfiguration vmHostAgentConfig) =>
            from resolvedPath in names.ResolveVmStorageBasePath(vmHostAgentConfig)
            from importVhdPath in names.ResolveVolumeStorageBasePath(vmHostAgentConfig)
            from _ in ComparePath(resolvedPath, vmPath, storageIdentifier)
            select new VMStorageSettings
            {
                StorageNames = names,
                StorageIdentifier = storageIdentifier,
                VMPath = vmPath,
                DefaultVhdPath = importVhdPath,
            };

        public static EitherAsync<Error, VMStorageSettings> FromCatletConfig(
            CatletConfig config,
            VmHostAgentConfiguration vmHostAgentConfig) =>
            // TODO Make these fail with an error. The Con
            from projectName in ProjectName.NewEither(config.Project)
                .MapLeft(e => Error.New($"The configured project of the catlet {config.Name} is invalid.", e))
                .ToAsync()
            from environmentName in EnvironmentName.NewEither(config.Environment)
                .MapLeft(e => Error.New($"The configured environment of the catlet {config.Name} is invalid.", e))
                .ToAsync()
            from dataStoreName in DataStoreName.NewEither(config.Store)
                .MapLeft(e => Error.New($"The configured data store of the catlet {config.Name} is invalid.", e))
                .ToAsync()
            from storageIdentifier in Optional(config.Location).Filter(notEmpty)
                .Map(n => ConfigModel.StorageIdentifier.NewEither(n).ToAsync())
                .Sequence()
            let storageNames = new StorageNames()
            {
                ProjectName = projectName.Value,
                EnvironmentName = environmentName.Value,
                DataStoreName = dataStoreName.Value,
            }
            from dataPath in storageNames.ResolveVmStorageBasePath(vmHostAgentConfig)
            from importVhdPath in storageNames.ResolveVolumeStorageBasePath(vmHostAgentConfig)
            select new VMStorageSettings
            {
                StorageNames = storageNames,
                StorageIdentifier = storageIdentifier.Map(s => s.Value),
                VMPath = dataPath,
                DefaultVhdPath = importVhdPath
            };

        public static EitherAsync<Error, VMStorageSettings> Plan(
            VmHostAgentConfiguration vmHostAgentConfig,
            CatletConfig config,
            VMStorageSettings currentStorageSettings) =>
            from newSettings in FromCatletConfig(config, vmHostAgentConfig)
            from result in currentStorageSettings.Frozen
                ? RightAsync<Error, VMStorageSettings>(currentStorageSettings)
                : from storageId in (newSettings.StorageIdentifier | currentStorageSettings.StorageIdentifier)
                      .ToEitherAsync(Error.New("No storage ID found for the virtual machine."))
                  select newSettings with { StorageIdentifier = storageId }
            select result;

        private static EitherAsync<Error, string> ComparePath(
            string firstPath,
            string secondPath,
            Option<string> storageIdentifier) =>
            from validStorageId in storageIdentifier.ToEither(Error.New("unknown VM storage identifier"))
                .ToAsync()
            let fullPath = Path.Combine(firstPath, validStorageId)
            from _ in guard(string.Equals(fullPath, secondPath, StringComparison.OrdinalIgnoreCase),
                Error.New("Path calculation failure"))
            select firstPath;
    }
}
