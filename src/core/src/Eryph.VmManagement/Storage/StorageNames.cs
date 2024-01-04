using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

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

        public static (StorageNames Names, Option<string> StorageIdentifier) FromPath(
            string path,
            VmHostAgentConfiguration vmHostAgentConfig,
            string defaultPath)
        {
            var projectName = Prelude.Some("default");
            var environmentName = Option<string>.None;
            var dataStoreName = Option<string>.None;
            var dataStorePath = Option<string>.None;
            var storageIdentifier = Option<string>.None;

            if (path.StartsWith(defaultPath, StringComparison.InvariantCultureIgnoreCase))
            {
                environmentName = Prelude.Some("default");
                dataStoreName = Prelude.Some("default");
                dataStorePath = defaultPath;
            }
            else
            {
                // TODO better validation
                // TODO functional programming
                var result = vmHostAgentConfig.Environments
                    ?.SelectMany(e => e.DataStores, (e, ds) => (e, ds))
                    ?.FirstOrDefault(r => path.StartsWith(r.ds.Path, StringComparison.InvariantCultureIgnoreCase));
                if (result.HasValue)
                {
                    environmentName = result.Value.e.Name;
                    dataStoreName = result.Value.ds.Name;
                    dataStorePath = result.Value.ds.Path;
                }
            }

            if (dataStorePath.IsSome)
            {
                var pathAfterDataStore = path.Remove(0, dataStorePath.ValueUnsafe().Length).TrimStart('\\');

                var pathRoot = pathAfterDataStore.Split(Path.DirectorySeparatorChar).FirstOrDefault() ??
                               pathAfterDataStore;

                if (pathRoot.StartsWith("p_"))
                {
                    projectName = pathRoot.Remove(0, "p_".Length).ToLowerInvariant();
                    pathAfterDataStore = pathAfterDataStore.Remove(0,pathRoot.Length + 1); // remove root + \\;
                }

                var lastPart = pathAfterDataStore
                    .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                if (lastPart != null && lastPart.Contains('.')) // assuming a filename if this contains a dot
                {
                    pathAfterDataStore  = pathAfterDataStore.Remove(pathAfterDataStore.LastIndexOf(lastPart,
                        StringComparison.InvariantCulture)).TrimEnd('\\');

                }


                var idCandidate = pathAfterDataStore;
                var idIsGeneRef = false;
                if (idCandidate.Contains(Path.DirectorySeparatorChar))
                {
                    // genepool path, resolve back to referenced geneset
                    if (idCandidate.StartsWith("genepool\\", StringComparison.InvariantCultureIgnoreCase))
                    {
                        idCandidate = idCandidate.Remove(0, "genepool\\".Length);
                        var genePathParts = idCandidate.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                        if (genePathParts.Length == 4)
                        {
                            idCandidate = genePathParts[0] + "/" + genePathParts[1]+ "/" + genePathParts[2];
                            idIsGeneRef = true;
                        }
                    }

                    idCandidate = idIsGeneRef ? $"gene:{idCandidate}:{Path.GetFileNameWithoutExtension(lastPart)}" : null;
                }

                if (!string.IsNullOrWhiteSpace(idCandidate))
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

        public EitherAsync<Error, string> ResolveStorageBasePath(VmHostAgentConfiguration vmHostAgentConfig, string defaultPath)
        {
            var names = this;
            return
                from dsName in DataStoreName.ToEitherAsync(
                    Error.New("Unknown data store name. Cannot resolve path"))
                from projectName in names.ProjectName.ToEitherAsync(Error.New(
                    "Unknown project name. Cannot resolve path"))
                from environmentName in names.EnvironmentName.ToEitherAsync(Error.New(
                    "Unknown environment name. Cannot resolve path"))
                from dsPath in LookupVMDatastorePathInEnvironment(dsName, environmentName, vmHostAgentConfig, defaultPath).ToAsync()
                from projectPath in JoinPathAndProject(dsPath, projectName)
                select projectPath;
        }

        private static async Task<Either<Error, string>> LookupVMDatastorePathInEnvironment(
            string datastore, string environment, VmHostAgentConfiguration vmHostAgentConfig, string defaultPath)
        {
            if (datastore == "default" && environment == "default")
                return defaultPath;

            var environmentConfig = vmHostAgentConfig.Environments?.FirstOrDefault(e => e.Name == environment);
            if (environmentConfig is null)
                return Error.New("Environment is not configured. Cannot resolve path");

            var dataStoreConfig = environmentConfig.DataStores?.FirstOrDefault(ds => ds.Name == datastore);
            if (dataStoreConfig is null)
                return Error.New("Data store is not configured. Cannot resolve path");

            if (string.IsNullOrWhiteSpace(dataStoreConfig.Path))
                return Error.New("The path of the data store is invalid");

            return dataStoreConfig.Path;
        }


        private static EitherAsync<Error, string> JoinPathAndProject(string dsPath, string projectName)
        {
            var result = dsPath;

            if (projectName != "default")
                result = Path.Combine(dsPath, $"p_{projectName}");

            return Prelude.RightAsync<Error, string>(result);
        }
    }
}