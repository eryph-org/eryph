using System;
using System.Linq;
using Eryph.Resources;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Core.Tokens;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneIdentifier(GeneType GeneType, GeneSetIdentifier GeneSet, string Gene)
{
    public string Name => $"{GeneSet.Name}:{Gene}";

    public static Either<Error, GeneIdentifier> Parse(GeneType geneType, string geneName)
    {
        geneName = geneName.ToLowerInvariant();
        var geneNameParts = geneName.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (geneNameParts.Length != 3 && geneNameParts.Length != 2)
            return Error.New($"Invalid gene name '{geneName}'");

        if (geneNameParts[0] == "gene")
            geneNameParts = geneNameParts.Skip(1).ToArray();

        return GeneSetIdentifier.Parse(geneNameParts[0])
            .Map(geneset => new GeneIdentifier(geneType, geneset, geneNameParts[1].ToLowerInvariant()));

    }

    public override string ToString()
    {
        return $"{GeneType} {Name}";
    }
}