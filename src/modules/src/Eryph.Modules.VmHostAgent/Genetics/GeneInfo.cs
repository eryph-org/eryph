using System;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.VmHostAgent.Genetics;

/// <summary>
/// Contains information about a gene.
/// </summary>
/// <param name="Id"></param>
/// <param name="Hash">
/// The hash which uniquely identifies the gene. This value
/// includes the algorithm identifier and looks like this:
/// <code>sha1:abcd...</code>
/// </param>
/// <param name="MetaData"></param>
/// <param name="DownloadUris"></param>
/// <param name="DownloadExpires"></param>
/// <param name="LocalPath"></param>
/// <param name="MergedWithImage"></param>
public record GeneInfo(
    UniqueGeneIdentifier Id,
    string Hash,
    GeneManifestData? MetaData,
    GenePartDownloadUri[]? DownloadUris,
    DateTimeOffset DownloadExpires,
    string? LocalPath,
    bool MergedWithImage)
{
    public override string ToString() => Id.ToString();
}
