namespace Eryph.Modules.VmHostAgent.Images;

public record ImageInfo(ImageIdentifier Id, string LocalPath, ImageManifestData MetaData)
{
    public readonly ImageIdentifier Id = Id;
    public readonly string LocalPath = LocalPath;
    public readonly ImageManifestData MetaData = MetaData;
}