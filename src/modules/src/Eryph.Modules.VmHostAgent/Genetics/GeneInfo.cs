namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneInfo(GeneIdentifier GeneId, string Hash, string HashAlg, GeneManifestData? MetaData, string? LocalPath, bool MergedWithImage)
{
    public readonly GeneIdentifier GeneId = GeneId;
    public readonly string Hash = Hash;
    public readonly string HashAlg = HashAlg;
    public readonly string? LocalPath = LocalPath;
    public readonly GeneManifestData? MetaData = MetaData;
    public readonly bool MergedWithImage = MergedWithImage;

    public override string ToString()
    {
        return GeneId.ToString();
    }
}