using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public class LocalGenepoolReader(
    IFileSystemService fileSystem,
    VmHostAgentConfiguration agentConfiguration)
    : ILocalGenepoolReader
{
    public EitherAsync<Error, Option<GeneSetIdentifier>> GetGenesetReference(
        GeneSetIdentifier geneSetId) =>
        from _ in RightAsync<Error, Unit>(unit)
        let genePoolPath = GenePoolPaths.GetGenePoolPath(agentConfiguration)
        let geneSetManifestPath = GenePoolPaths.GetGeneSetManifestPath(genePoolPath, geneSetId)
        from genesetManifestValue in TryAsync(async () =>
        {
            var manifestJson = await fileSystem.ReadAllTextAsync(geneSetManifestPath);
            var manifest = JsonSerializer.Deserialize<JsonNode>(manifestJson);
            return Optional(manifest["ref"]?.GetValue<string>());
        }).ToEither(ex => Error.New($"Could not read manifest of geneset '{geneSetId}' from local genepool.", ex))
        from reference in genesetManifestValue
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .ToAsync()
        select reference;

    public EitherAsync<Error, string> ReadGeneContent(
        GeneType geneType,
        GeneArchitecture architecture,
        GeneIdentifier geneId) =>
        from _1 in guardnot(geneType is GeneType.Volume,
                Error.New($"The gene '{geneId}' is a volume gene."))
            .ToEitherAsync()
        from _2 in guard(geneType is GeneType.Catlet or GeneType.Fodder,
            Error.New($"The gene type '{geneType}' is not supported."))
        let genePoolPath = GenePoolPaths.GetGenePoolPath(agentConfiguration)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, architecture, geneId)
        from fileExists in Try(() => fileSystem.FileExists(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
            .ToAsync()
        from _ in guard(fileExists,
            Error.New($"Gene '{geneId}' does not exist in local genepool."))
        from content in TryAsync(() => fileSystem.ReadAllTextAsync(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{geneId}' from local genepool.", ex))
        select content;
}
