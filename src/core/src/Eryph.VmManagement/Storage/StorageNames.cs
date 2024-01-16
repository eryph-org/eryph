using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage
{
    public readonly struct StorageNames
    {
        public StorageNames()
        {
        }

        public Option<string> DataStoreName { get; init; } = "default";
        public Option<string> ProjectName { get; init; } = "default";
        public Option<string> EnvironmentName { get; init; } = "default";

        public bool IsValid => DataStoreName.IsSome && ProjectName.IsSome && EnvironmentName.IsSome;

        public static (StorageNames Names, Option<string> StorageIdentifier) FromVmPath(
            string path,
            VmHostAgentConfiguration vmHostAgentConfig)
        {
            return FromPath(path, vmHostAgentConfig, defaults => defaults.Vms);
        }

        public static (StorageNames Names, Option<string> StorageIdentifier) FromVhdPath(
            string path,
            VmHostAgentConfiguration vmHostAgentConfig)
        {
            return FromPath(path, vmHostAgentConfig, defaults => defaults.Volumes);
        }

        private static (StorageNames Names, Option<string> StorageIdentifier) FromPath(
            string path,
            VmHostAgentConfiguration vmHostAgentConfig,
            Func<VmHostAgentDefaultsConfiguration, string> getDefault)
        {
            var pathCandidates = vmHostAgentConfig.Environments.SelectMany(
                e => e.Datastores,
                (e, ds) => (Environment: e.Name, Datastore: ds.Name, ds.Path))
                .Concat(vmHostAgentConfig.Environments
                    .Select(e => (Environment: e.Name, Datastore: "default", Path: getDefault(e.Defaults))))
                .Concat(vmHostAgentConfig.Datastores.Select(ds => (Environment: "default", Datastore: ds.Name, ds.Path)))
                .Concat(new[] { (Environment: "default", Datastore: "default", Path: getDefault(vmHostAgentConfig.Defaults)) });

            var match = pathCandidates
                .Select(pc => from relativePath in GetContainedPath(pc.Path, path)
                              select (pc.Environment, pc.Datastore, RelativePath: relativePath))
                .Somes()
                .HeadOrNone();

            return match.Bind(e =>
                from root in e.RelativePath.Split(Path.DirectorySeparatorChar).HeadOrNone()
                select root.StartsWith("p_")
                    ? new
                    {
                        StorageNames = new StorageNames()
                        {
                            EnvironmentName = e.Environment,
                            DataStoreName = e.Datastore,
                            ProjectName = root.Remove(0, "p_".Length).ToLowerInvariant(),
                        },
                        RelativePath = Path.GetRelativePath(root + Path.DirectorySeparatorChar, e.RelativePath),
                    }
                    : new
                    {
                        StorageNames = new StorageNames()
                        {
                            EnvironmentName = e.Environment,
                            DataStoreName = e.Datastore,
                            ProjectName = "default",
                        },
                        e.RelativePath,
                    }
                ).Map(e =>
                {
                    var storageIdentifier = GetGeneReference(e.RelativePath).Match(
                        Some: v => v,
                        None: () => Path.HasExtension(e.RelativePath) ? Path.GetDirectoryName(e.RelativePath) : e.RelativePath);

                    return (e.StorageNames, string.IsNullOrWhiteSpace(storageIdentifier) ? None : Optional(storageIdentifier));
                }).Match(
                    Some: v => v,
                    None: () => (new StorageNames() { DataStoreName = None, EnvironmentName = None, ProjectName = None }, None));
        }

        public EitherAsync<Error, string> ResolveVmStorageBasePath(VmHostAgentConfiguration vmHostAgentConfig)
        {
            return from paths in ResolveStorageBasePaths(vmHostAgentConfig).ToAsync()
                   select paths.VmPath;
        }

        public EitherAsync<Error, string> ResolveVolumeStorageBasePath(VmHostAgentConfiguration vmHostAgentConfig)
        {
            return from paths in ResolveStorageBasePaths(vmHostAgentConfig).ToAsync()
                   select paths.VhdPath;
        }

        private static Option<string> GetContainedPath(string relativeTo, string path)
        {
            var relativePath = Path.GetRelativePath(relativeTo, path);
            if (relativePath.StartsWith("..") || relativePath.StartsWith(".") || relativePath == path)
                return None;

            return Some(relativePath);
        }

        private static Option<string> GetGeneReference(string relativePath)
        {
            return from genePath in GetContainedPath("genepool", relativePath)
                   let geneDirectory = Path.GetDirectoryName(genePath)
                   let genePathParts = geneDirectory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                   where genePathParts.Length == 4 && string.Equals(genePathParts[3], "volumes", StringComparison.OrdinalIgnoreCase)
                   select $"gene:{genePathParts[0]}/{genePathParts[1]}/{genePathParts[2]}:{Path.GetFileNameWithoutExtension(genePath)}";
        }

        private Either<Error, (string VmPath, string VhdPath)> ResolveStorageBasePaths(
            VmHostAgentConfiguration vmHostAgentConfig)
        {
            var names = this;
            return from dsName in names.DataStoreName.ToEither(Error.New("Unknown data store name. Cannot resolve path"))
                   from projectName in names.ProjectName.ToEither(Error.New("Unknown project name. Cannot resolve path"))
                   from environmentName in names.EnvironmentName.ToEither(Error.New("Unknown environment name. Cannot resolve path"))
                   from datastorePaths in LookupVMDatastorePathInEnvironment(dsName, environmentName, vmHostAgentConfig)
                   select (JoinPathAndProject(datastorePaths.VmPath, projectName), JoinPathAndProject(datastorePaths.VhdPath, projectName));
        }

        private static Either<Error, (string VmPath, string VhdPath )> LookupVMDatastorePathInEnvironment(
            string dataStore,
            string environment,
            VmHostAgentConfiguration vmHostAgentConfig)
        {
            return dataStore == "default"
                ? from defaults in environment == "default"
                    ? vmHostAgentConfig.Defaults
                    : from envConfig in vmHostAgentConfig.Environments.Where(e => e.Name == environment)
                        .HeadOrLeft(Error.New($"The environment {environment} is not configured"))
                      select envConfig.Defaults
                  select (defaults.Vms, defaults.Volumes)
                : from defaultDatastoreConfig in vmHostAgentConfig.Datastores.Where(ds => ds.Name == dataStore)
                    .HeadOrLeft(Error.New($"The datastore {dataStore} is not configured"))
                  let datastoreConfig = environment == "default"
                        ? defaultDatastoreConfig
                        : Prelude.match(from envConfig in vmHostAgentConfig.Environments
                        where envConfig.Name == environment
                        from envDsConfig in envConfig.Datastores
                        where envDsConfig.Name == dataStore
                        select envDsConfig, Empty: () => defaultDatastoreConfig, More: l => l.Head)
                  select (datastoreConfig.Path, datastoreConfig.Path);
        }


        private static string JoinPathAndProject(string dsPath, string projectName)
        {
            return projectName == "default" ? dsPath : Path.Combine(dsPath, $"p_{projectName}");
        }
    }
}