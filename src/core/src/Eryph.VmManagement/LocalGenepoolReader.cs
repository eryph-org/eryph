using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
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
                var genesetManifestPath = Path.Combine(GetGeneSetPath(geneset), "geneset-tag.json");
                var manifest = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(genesetManifestPath));
                var reference = manifest["ref"]?.GetValue<string>() ?? "";
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    return reference;
                }
                return Option<string>.None;
            }).ToEither(Error.New)
            from reference in genesetManifest.Match(
                Some: s => GeneSetIdentifier.NewEither(s).Map(Option<GeneSetIdentifier>.Some),
                None: () => Option<GeneSetIdentifier>.None
            )
            select reference;
    }

    public Either<Error, string> ReadGeneContent(
        GeneType geneType,
        GeneIdentifier geneIdentifier)
    {
        return Prelude.Try(() =>
        {
            var geneFolder = geneType switch
            {
                GeneType.Catlet => ".",
                GeneType.Volume => "volumes",
                GeneType.Fodder => "fodder",
                _ => throw new ArgumentOutOfRangeException()
            };
            var genePath = Path.Combine(GetGeneSetPath(geneIdentifier.GeneSet), geneFolder,
                $"{geneIdentifier.GeneName}.json");
            if (!File.Exists(genePath))
                throw new InvalidDataException($"Gene '{geneIdentifier}' not found in local genepool.");

            return File.ReadAllText(genePath);

        }).ToEither(Error.New);
    }

    private string GetGeneSetPath(GeneSetIdentifier geneSetIdentifier) =>
        Path.Combine(_agentConfiguration.Defaults.Volumes,
            "genepool",
            geneSetIdentifier.Organization.Value,
            geneSetIdentifier.GeneSet.Value,
            geneSetIdentifier.Tag.Value);
}