using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

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
            VmHostAgentConfiguration vmHostAgentConfig) =>
            GetGeneReference(vmHostAgentConfig, path).Match(
                Some: geneIdentifier => (
                    new StorageNames()
                    {
                        DataStoreName = "default",
                        EnvironmentName = "default",
                        ProjectName = "default",
                    },
                    Some(geneIdentifier.Value)),
                None: () => FromPath(path, vmHostAgentConfig, defaults => defaults.Volumes));

        private static (StorageNames Names, Option<string> StorageIdentifier) FromPath(
            string path,
            VmHostAgentConfiguration vmHostAgentConfig,
            Func<VmHostAgentDefaultsConfiguration, string> getDefault) =>
            match(
                from pathCandidate in Seq(
                        vmHostAgentConfig.Environments.ToSeq()
                            .SelectMany(e => e.Datastores.ToSeq(),
                                (e, ds) => (Environment: e.Name, Datastore: ds.Name, ds.Path)),
                        vmHostAgentConfig.Environments.ToSeq()
                            .Select(e => (Environment: e.Name, Datastore: "default", Path: getDefault(e.Defaults))),
                        vmHostAgentConfig.Datastores.ToSeq()
                            .Select(ds => (Environment: "default", Datastore: ds.Name, ds.Path)),
                        Seq1((Environment: "default", Datastore: "default",
                            Path: getDefault(vmHostAgentConfig.Defaults))))
                    .Flatten()
                    .Select(pc => from relativePath in GetContainedPath(pc.Path, path)
                        select (pc.Environment, pc.Datastore, RelativePath: relativePath))
                    .Somes()
                    .HeadOrNone()
                from root in pathCandidate.RelativePath.Split(Path.DirectorySeparatorChar).HeadOrNone()
                from projectName in root.StartsWith("p_")
                    ? ConfigModel.ProjectName.NewOption(root.Remove(0, "p_".Length))
                    : ConfigModel.ProjectName.New("default")
                let remainingPath = projectName.Value != "default"
                    ? Path.GetRelativePath(root + Path.DirectorySeparatorChar, pathCandidate.RelativePath)
                    : pathCandidate.RelativePath
                // VM paths only point to a directory instead of a specific file. Hence, the path can end with
                // a storage identifier.
                from storageIdentifier in match(
                    Optional(Path.HasExtension(remainingPath) ? Path.GetDirectoryName(remainingPath) : remainingPath).Filter(notEmpty),
                    Some: s => from si in StorageIdentifier.NewOption(s)
                               select Some(si.Value),
                    None: () => Some(Option<string>.None))
                select (
                    new StorageNames()
                    {
                        DataStoreName = pathCandidate.Datastore,
                        EnvironmentName = pathCandidate.Environment,
                        ProjectName = projectName.Value,
                    },
                    storageIdentifier),
                Some: v => v,
                None: () => (new StorageNames() { DataStoreName = None, EnvironmentName = None, ProjectName = None }, None));
        

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

        private static Option<GeneIdentifier> GetGeneReference(
            VmHostAgentConfiguration vmHostAgentConfig,
            string vhdPath) =>
            from genePath in GetContainedPath(Path.Combine(vmHostAgentConfig.Defaults.Volumes, "genepool"), vhdPath)
            let geneDirectory = Path.GetDirectoryName(genePath)
            let genePathParts = geneDirectory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            where genePathParts.Length == 4
            where string.Equals(genePathParts[3], "volumes", StringComparison.OrdinalIgnoreCase)
            let geneFileName = Path.GetFileNameWithoutExtension(genePath)
            from geneIdentifer in GeneIdentifier.NewOption(
                $"gene:{genePathParts[0]}/{genePathParts[1]}/{genePathParts[2]}:{geneFileName}")
            select geneIdentifer;
        

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
                    : from envConfig in Optional(vmHostAgentConfig.Environments).ToSeq().Flatten()
                        .Where(e => e.Name == environment)
                        .HeadOrLeft(Error.New($"The environment {environment} is not configured"))
                      select envConfig.Defaults
                  select (defaults.Vms, defaults.Volumes)
                : from defaultDatastoreConfig in Optional(vmHostAgentConfig.Datastores).ToSeq().Flatten()
                    .Where(ds => ds.Name == dataStore)
                    .HeadOrLeft(Error.New($"The datastore {dataStore} is not configured"))
                  let datastoreConfig = environment == "default"
                        ? defaultDatastoreConfig
                        : match(from envConfig in vmHostAgentConfig.Environments
                                where envConfig.Name == environment
                                from envDsConfig in envConfig.Datastores
                                where envDsConfig.Name == dataStore
                                select envDsConfig,
                                Empty: () => defaultDatastoreConfig,
                                More: l => l.Head)
                  select (datastoreConfig.Path, datastoreConfig.Path);
        }

        private static string JoinPathAndProject(string dsPath, string projectName)
        {
            return projectName == "default" ? dsPath : Path.Combine(dsPath, $"p_{projectName}");
        }
    }
}