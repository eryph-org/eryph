using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.Core.VmAgent;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public class LocalGenepoolReader : ILocalGenepoolReader
{
    private readonly VmHostAgentConfiguration _agentConfiguration;

    public LocalGenepoolReader(VmHostAgentConfiguration agentConfiguration)
    {
        _agentConfiguration = agentConfiguration;
    }

    public Either<Error, Option<GeneSetIdentifier>> GetGenesetReference(GeneSetIdentifier geneset)
    {
        return from genesetManifest in Prelude.Try(() =>
            {
                var genepoolPath = Path.Combine(_agentConfiguration.Defaults.Volumes, "genepool");
                var pathName = geneset.Name.Replace('/', '\\');
                var genesetManifestPath = Path.Combine(genepoolPath, pathName, "geneset.json");
                var manifest = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(genesetManifestPath));
                var reference = manifest["ref"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    return reference;
                }
                return Option<string>.None;
            }).ToEither(Error.New)
            from reference in genesetManifest.Match(
                Some: s => GeneSetIdentifier.Parse(s).Map(Option<GeneSetIdentifier>.Some),
                None: () => Option<GeneSetIdentifier>.None
            )
            select reference;
    }

    public Either<Error, string> ReadGeneContent(GeneIdentifier geneIdentifier)
    {
        return Prelude.Try(() =>
        {
            var genepoolPath = Path.Combine(_agentConfiguration.Defaults.Volumes, "genepool");
            var pathName = geneIdentifier.GeneSet.Name.Replace('/', '\\');
            var fodderGenePath = Path.Combine(genepoolPath, pathName, "fodder",
                $"{geneIdentifier.Gene}.json");
            if (!File.Exists(fodderGenePath))
                throw new InvalidDataException($"Geneset '{geneIdentifier.GeneSet}' not found in local genepool.");

            return File.ReadAllText(fodderGenePath);

        }).ToEither(Error.New);
    }
}