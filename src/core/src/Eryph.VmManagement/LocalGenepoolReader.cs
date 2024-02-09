using System;
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
                var genesetManifestPath = Path.Combine(genepoolPath, pathName, "geneset-tag.json");
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

            var geneFolder = geneIdentifier.GeneType switch
            {
                GeneType.Catlet => ".",
                GeneType.Volume => "volumes",
                GeneType.Fodder => "fodder",
                _ => throw new ArgumentOutOfRangeException()
            };
            var genePath = Path.Combine(genepoolPath, pathName, geneFolder,
                $"{geneIdentifier.Gene}.json");
            if (!File.Exists(genePath))
                throw new InvalidDataException($"Gene '{geneIdentifier}' not found in local genepool.");

            return File.ReadAllText(genePath);

        }).ToEither(Error.New);
    }
}