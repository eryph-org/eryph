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
        from _ in Right<Error, Unit>(unit)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(agentConfiguration)
        let geneSetPath = GenePoolPaths.GetGeneSetPath(genePoolPath, geneSetId)
        from genesetManifestValue in Try(() =>
        {
            var genesetManifestPath = Path.Combine(geneSetPath, "geneset-tag.json");
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
        from _1 in guardnot(geneType is GeneType.Volume,
            Error.New($"The gene '{geneId}' is a volume gene."))
            .ToEither()
        from _2 in guard(geneType is GeneType.Catlet or GeneType.Fodder,
            Error.New($"The gene type '{geneType}' is not supported."))
        let genePoolPath = GenePoolPaths.GetGenePoolPath(agentConfiguration)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, geneId)
        from fileExists in Try(() => File.Exists(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
        from _ in guard(fileExists,
            Error.New($"Gene '{geneId}' does not exist in local genepool."))
        from content in Try(() => File.ReadAllText(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
        select content;
}
