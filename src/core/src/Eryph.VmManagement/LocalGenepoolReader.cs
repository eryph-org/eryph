using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public class LocalGenepoolReader(VmHostAgentConfiguration agentConfiguration)
    : ILocalGenepoolReader
{
    public Either<Error, Option<GeneSetIdentifier>> GetGenesetReference(
        GeneSetIdentifier geneSetId) =>
        from genesetManifestValue in Try(() =>
        {
            var genesetManifestPath = Path.Combine(GetGeneSetPath(geneSetId), "geneset-tag.json");
            var manifest = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(genesetManifestPath));
            return Optional(manifest["ref"]?.GetValue<string>());
        }).ToEither(ex => Error.New($"Could not read manifest of geneset '{geneSetId}' from local genepool.", ex))
        from reference in genesetManifestValue
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
        select reference;

    public Either<Error, string> ReadGeneContent(
        GeneType geneType,
        GeneIdentifier geneId) =>
        from geneFolder in geneType switch
        {
            GeneType.Catlet => Right<Error, string>("."),
            GeneType.Volume => "volumes",
            GeneType.Fodder => "fodder",
            _ => Error.New($"Could not read gene '{geneId}' from local genepool. The gene type '{geneType}' is not supported.")
        }
        let genePath = Path.Combine(GetGeneSetPath(geneId.GeneSet), geneFolder,
            $"{geneId.GeneName}.json")
        from fileExists in Try(() => File.Exists(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
        from _ in guard(fileExists,
            Error.New($"Gene '{geneId}' does not exist in local genepool."))
        from content in Try(() => File.ReadAllText(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
        select content;

    private string GetGeneSetPath(GeneSetIdentifier geneSetIdentifier) =>
        Path.Combine(agentConfiguration.Defaults.Volumes,
            "genepool",
            geneSetIdentifier.Organization.Value,
            geneSetIdentifier.GeneSet.Value,
            geneSetIdentifier.Tag.Value);
}
