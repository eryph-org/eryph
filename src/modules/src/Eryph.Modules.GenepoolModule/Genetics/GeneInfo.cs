using System;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.Genepool.Genetics;

/// <summary>
/// Contains information about a gene.
/// </summary>
/// <param name="Hash">
/// The hash which uniquely identifies the gene. This value
/// includes the algorithm identifier and looks like this:
/// <c>sha1:abcd...</c>
/// </param>
public record GeneInfo(
    UniqueGeneIdentifier Id,
    string Hash,
    GeneManifestData? MetaData,
    GenePartDownloadUri[]? DownloadUris,
    DateTimeOffset DownloadExpires,
    bool MergedWithImage)
{
    public override string ToString() => Id.ToString();
}
