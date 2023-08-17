using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

namespace Eryph.VmManagement.Storage
{
    public class StorageNames
    {
        public Option<string> DataStoreName { get; set; }
        public Option<string> ProjectName { get; set; }
        public Option<string> EnvironmentName { get; set; }

        public static (StorageNames Names, Option<string> StorageIdentifier) FromPath(string path, string defaultPath)
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

                    idCandidate = idIsGeneRef ? $"gene:{idCandidate}" : null;
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

        public EitherAsync<Error, string> ResolveStorageBasePath(string defaultPath)
        {
            return
                from dsName in DataStoreName.ToEitherAsync(
                    Error.New("Unknown data store name. Cannot resolve path"))
                from projectName in ProjectName.ToEitherAsync(Error.New(
                    "Unknown project name. Cannot resolve path"))
                from environmentName in EnvironmentName.ToEitherAsync(Error.New(
                    "Unknown environment name. Cannot resolve path"))
                from dsPath in LookupVMDatastorePathInEnvironment(dsName, environmentName, defaultPath).ToAsync()
                from projectPath in JoinPathAndProject(dsPath, projectName)
                select projectPath;

        }

        private static async Task<Either<Error, string>> LookupVMDatastorePathInEnvironment(
            string datastore, string environment, string defaultPath)
        {
            return defaultPath;
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