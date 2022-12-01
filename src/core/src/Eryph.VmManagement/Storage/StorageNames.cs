using System;
using System.IO;
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

                var pathRoot = Path.GetPathRoot(pathAfterDataStore);
                if (pathRoot.StartsWith("eryph_p")) projectName = pathRoot.Remove(0, "eryph_p".Length);

                var idCandidate = pathAfterDataStore;

                if (idCandidate.Contains(Path.DirectorySeparatorChar))
                    idCandidate = Path.GetDirectoryName(pathAfterDataStore);

                if (!string.IsNullOrWhiteSpace(idCandidate) && pathRoot != idCandidate)
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
                result = Path.Combine(dsPath, $"eryph_p{projectName}");

            return Prelude.RightAsync<Error, string>(result);
        }
    }
}