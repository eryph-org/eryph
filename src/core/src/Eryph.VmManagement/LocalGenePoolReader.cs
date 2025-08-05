using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public class LocalGenePoolReader(
    IFileSystemService fileSystem,
    string genePoolPath)
    : ILocalGenePoolReader
{
    public EitherAsync<Error, Option<GeneSetIdentifier>> GetGenesetReference(
        GeneSetIdentifier geneSetId) =>
        from _ in RightAsync<Error, Unit>(unit)
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
        UniqueGeneIdentifier uniqueGeneId) =>
        from _1 in guardnot(uniqueGeneId.GeneType is GeneType.Volume,
                Error.New($"The gene '{uniqueGeneId}' is a volume gene."))
            .ToEitherAsync()
        from _2 in guard(uniqueGeneId.GeneType is GeneType.Catlet or GeneType.Fodder,
            Error.New($"The gene type '{uniqueGeneId.GeneType}' is not supported."))
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, uniqueGeneId)
        from fileExists in Try(() => fileSystem.FileExists(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{uniqueGeneId}' from local genepool.", ex))
            .ToAsync()
        from _ in guard(fileExists,
            Error.New($"Gene '{uniqueGeneId}' does not exist in local genepool."))
        from content in TryAsync(() => fileSystem.ReadAllTextAsync(genePath))
            .ToEither(ex => Error.New($"Could not read gene '{uniqueGeneId}' from local genepool.", ex))
        select content;
}
