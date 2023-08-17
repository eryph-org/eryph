using System;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneSetIdentifier(string Organization, string GeneSet, string Tag)
{
    public readonly string Organization = Organization;
    public readonly string GeneSet = GeneSet;
    public readonly string Tag = Tag;

    public string Name => $"{Organization}/{GeneSet}/{Tag}";
    public string UntaggedName => $"{Organization}/{GeneSet}";

    public static Either<Error, GeneSetIdentifier> Parse(string genesetName)
    {
        genesetName = genesetName.ToLowerInvariant();
        var imageParts = genesetName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (imageParts.Length != 3 && imageParts.Length != 2)
            return Error.New($"Invalid geneset name '{genesetName}'");

        var org = imageParts[0].ToLowerInvariant();
        var id = imageParts[1].ToLowerInvariant();
        var tag = imageParts.Length == 3 ? imageParts[2].ToLowerInvariant() : "latest";

        return new GeneSetIdentifier(org, id, tag);
    }

    public override string ToString()
    {
        return Name;
    }
}