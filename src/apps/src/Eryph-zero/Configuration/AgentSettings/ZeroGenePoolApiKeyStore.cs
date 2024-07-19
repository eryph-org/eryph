using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Configuration.AgentSettings;

internal class ZeroGenePoolApiKeyStore : IGenePoolApiKeyStore
{
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
            var path = Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "genepool-keys.json");
            if (!File.Exists(path))
                return None;

            var json = await File.ReadAllTextAsync(path);
            return json;
        }).ToEither(ex => Error.New("Could not access gene pool API key store.", ex))
        from apiKeys in json.Map(Deserialize).Sequence().ToAsync()
        select apiKeys.IfNone(HashMap<string, GenePoolApiKey>());

    private static Either<Error, HashMap<string, GenePoolApiKey>> Deserialize(string json) =>
        from optionalDictionary in Try(() =>
            Optional(JsonSerializer.Deserialize<IReadOnlyDictionary<string, GenePoolApiKey>>(json)))
            .ToEither(ex => Error.New("Could not deserialize contents of the gene pool key store", ex))
        from dictionary in optionalDictionary.ToEither(
            Error.New("Could not deserialize contents of the gene pool key store"))
        select dictionary.ToHashMap();

    private static string Serialize(HashMap<string, GenePoolApiKey> apiKeys) =>
        JsonSerializer.Serialize(apiKeys.ToDictionary());

    private static EitherAsync<Error, Unit> WriteStore(HashMap<string, GenePoolApiKey> apiKeys) =>
        from _ in RightAsync<Error, Unit>(unit)
        let json = Serialize(apiKeys)
        from __ in TryAsync(async () =>
        {
            var path = Path.Combine(ZeroConfig.GetVmHostAgentConfigPath(), "genepool-keys.json");
            await File.WriteAllTextAsync(path, json);
            return unit;
        }).ToEither(ex => Error.New("Could not write to gene pool API key store.", ex))
        select unit;
}