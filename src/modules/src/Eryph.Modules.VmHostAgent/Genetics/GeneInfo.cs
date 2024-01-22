using System;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneInfo(GeneIdentifier GeneId, string Hash, string HashAlg, 
    GeneManifestData? MetaData,
    GenePartDownloadUri[]? DownloadUris,
    DateTimeOffset DownloadExpires,
    string? LocalPath, bool MergedWithImage)
{
    public readonly GeneIdentifier GeneId = GeneId;
    public readonly string Hash = Hash;
    public readonly string HashAlg = HashAlg;
    public readonly string? LocalPath = LocalPath;
    public readonly GeneManifestData? MetaData = MetaData;
    public readonly bool MergedWithImage = MergedWithImage;
    public readonly GenePartDownloadUri[]? DownloadUris = DownloadUris;
    public readonly DateTimeOffset DownloadExpires = DownloadExpires;

    public override string ToString()
    {
        return GeneId.ToString();
    }
}