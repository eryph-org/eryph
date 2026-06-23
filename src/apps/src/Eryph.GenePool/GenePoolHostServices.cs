using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.AppCore;
using Eryph.Core;
using Eryph.Modules.GenePool.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.GenePool
{
    /// <summary>
    /// Standalone-runtime implementation of <see cref="IApplicationInfoProvider"/>
    /// (the gene pool agent's counterpart to eryph-zero's provider).
    /// </summary>
    internal sealed class GenePoolApplicationInfoProvider : IApplicationInfoProvider
    {
        public GenePoolApplicationInfoProvider()
        {
            Name = "eryph-genepool";
            var entryAssembly = Assembly.GetEntryAssembly()!;
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
            ProductVersion = fileVersionInfo.ProductVersion ?? "unknown";
            // Truncated to at most 24 characters for compatibility with AutoRest (the version
            // may be shorter than 24 chars, e.g. when ProductVersion is missing).
            var applicationId = $"genepool-{ProductVersion}";
            ApplicationId = applicationId[..Math.Min(24, applicationId.Length)];
        }

        public string Name { get; }
        public string ProductVersion { get; }
        public string ApplicationId { get; set; }
    }

    /// <summary>
    /// Reads/writes the gene pool API keys from the standalone component config root. Mirrors
    /// eryph-zero's <c>ZeroGenePoolApiKeyStore</c> but uses <see cref="AppConfigPaths"/> instead of
    /// the fixed eryph-zero config path, so the key store honours <c>ERYPH_CONFIG_PATH</c>.
    /// </summary>
    internal sealed class GenePoolApiKeyStore : IGenePoolApiKeyStore
    {
        private static string StorePath =>
            Path.Combine(AppConfigPaths.GetVmHostAgentConfigPath(), "genepool-keys.json");

        public EitherAsync<Error, Option<GenePoolApiKey>> GetApiKey(string genePoolName) =>
            from store in ReadStore()
            select store.Find(genePoolName);

        public EitherAsync<Error, HashMap<string, GenePoolApiKey>> GetApiKeys() =>
            from store in ReadStore()
            select store;

        public EitherAsync<Error, Unit> SaveApiKey(string genePoolName, GenePoolApiKey apiKey) =>
            from store in ReadStore()
            let updatedStore = store.AddOrUpdate(genePoolName, apiKey)
            from _ in WriteStore(updatedStore)
            select unit;

        public EitherAsync<Error, Unit> RemoveApiKey(string genePoolName) =>
            from store in ReadStore()
            let updatedStore = store.Remove(genePoolName)
            from _ in WriteStore(updatedStore)
            select unit;

        private static EitherAsync<Error, HashMap<string, GenePoolApiKey>> ReadStore() =>
            from json in TryAsync<Option<string>>(async () =>
            {
                var path = StorePath;
                if (!File.Exists(path))
                    return None;

                return await File.ReadAllTextAsync(path);
            }).ToEither(ex => Error.New("Could not access the gene pool API key store.", ex))
            from apiKeys in json.Map(Deserialize).Sequence().ToAsync()
            select apiKeys.IfNone(HashMap<string, GenePoolApiKey>());

        private static Either<Error, HashMap<string, GenePoolApiKey>> Deserialize(string json) =>
            from optionalDictionary in Try(() =>
                Optional(JsonSerializer.Deserialize<IReadOnlyDictionary<string, GenePoolApiKey>>(json)))
                .ToEither(ex => Error.New("Could not deserialize the contents of the gene pool key store.", ex))
            from dictionary in optionalDictionary.ToEither(
                Error.New("Could not deserialize the contents of the gene pool key store."))
            select dictionary.ToHashMap();

        private static string Serialize(HashMap<string, GenePoolApiKey> apiKeys) =>
            JsonSerializer.Serialize(apiKeys.ToDictionary());

        private static EitherAsync<Error, Unit> WriteStore(HashMap<string, GenePoolApiKey> apiKeys) =>
            from _ in RightAsync<Error, Unit>(unit)
            let json = Serialize(apiKeys)
            from __ in TryAsync(async () =>
            {
                await File.WriteAllTextAsync(StorePath, json);
                return unit;
            }).ToEither(ex => Error.New("Could not write to the gene pool API key store.", ex))
            select unit;
    }
}
