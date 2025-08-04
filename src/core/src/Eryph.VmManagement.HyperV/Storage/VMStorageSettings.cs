using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.VmAgent;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.SomeHelp;

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

        public static EitherAsync<Error, Option<VMStorageSettings>> FromVM(
            VmHostAgentConfiguration vmHostAgentConfig,
            TypedPsObject<VirtualMachineInfo> vm)
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(vm.Value.Path, vmHostAgentConfig);

            async Task<VMStorageSettings> FromVMAsync()
            {
                return await
                    (from resolvedPath in names.ResolveVmStorageBasePath(vmHostAgentConfig)
                        from importVhdPath in names.ResolveVolumeStorageBasePath(vmHostAgentConfig)
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

            return RightAsync<Error, VMStorageSettings>(FromVMAsync())
                .Map(r => r.ToSome().ToOption());
        }


        public static EitherAsync<Error, VMStorageSettings> FromCatletConfig(
            CatletConfig config,
            VmHostAgentConfiguration vmHostAgentConfig) =>
            from projectName in Optional(config.Project).Filter(notEmpty).Match(
                Some: n => ProjectName.NewEither(n).ToAsync(),
                None: () => RightAsync<Error, ProjectName>(ProjectName.New("default")))
            from environmentName in Optional(config.Environment).Filter(notEmpty).Match(
                Some: n => EnvironmentName.NewEither(n).ToAsync(),
                None: () => RightAsync<Error, EnvironmentName>(EnvironmentName.New("default")))
            from dataStoreName in Optional(config.Store).Filter(notEmpty).Match(
                Some: n => DataStoreName.NewEither(n).ToAsync(),
                None: () => RightAsync<Error, DataStoreName>(DataStoreName.New("default")))
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
            string newStorageId,
            CatletConfig config,
            Option<VMStorageSettings> currentStorageSettings) =>
            from newSettings in FromCatletConfig(config, vmHostAgentConfig)
            let frozenSettings = currentStorageSettings
                .Filter(s => s.Frozen)
            let storageId = (newSettings.StorageIdentifier | currentStorageSettings.Bind(s => s.StorageIdentifier))
                .IfNone(newStorageId)
            let result = frozenSettings.Match(
                Some: s => s,
                None: () => newSettings with { StorageIdentifier = storageId })
            select result;

        private static EitherAsync<Error, string> ComparePath(string firstPath, string secondPath,
            Option<string> storageIdentifier)
        {
            return storageIdentifier.ToEither(Error.New("unknown VM storage identifier"))
                .ToAsync()
                .Bind(id =>
                {
                    var fullPath = Path.Combine(firstPath, id);

                    if (!secondPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                        return LeftAsync<Error, string>(Error.New(
                                "Path calculation failure"));

                    return RightAsync<Error, string>(firstPath);
                });
        }
    }
}