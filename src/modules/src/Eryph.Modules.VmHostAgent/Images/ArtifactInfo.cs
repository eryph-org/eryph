using JetBrains.Annotations;

namespace Eryph.Modules.VmHostAgent.Images;

public record ArtifactInfo(ImageIdentifier ImageId, string Hash, string HashAlg, ArtifactManifestData MetaData, string LocalPath, bool MergedWithImage)
{
    public readonly ImageIdentifier ImageId = ImageId;
    public readonly string Hash = Hash;
    public readonly string HashAlg = HashAlg;
    public readonly string LocalPath = LocalPath;
    [CanBeNull] public readonly ArtifactManifestData MetaData = MetaData;
    public readonly bool MergedWithImage = MergedWithImage;

    public override string ToString()
    {
        return $"{ImageId.Name}/{Hash[..12]}";
    }
}