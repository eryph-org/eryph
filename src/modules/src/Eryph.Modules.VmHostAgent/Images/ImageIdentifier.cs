using System;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public record ImageIdentifier(string Organization, string ImageId, string Tag)
{
    public readonly string Organization = Organization;
    public readonly string ImageId = ImageId;
    public readonly string Tag = Tag;

    public string Name => $"{Organization}/{ImageId}/{Tag}";

    public static Either<PowershellFailure, ImageIdentifier> Parse(string imageName)
    {
        imageName = imageName.ToLowerInvariant();
        var imageParts = imageName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (imageParts.Length != 3 && imageParts.Length != 2)
            return new PowershellFailure { Message = $"Invalid image name '{imageName}'" };

        var imageOrg = imageParts[0];
        var imageId = imageParts[1];
        var imageTag = imageParts.Length == 3 ? imageParts[2] : "latest";

        return new ImageIdentifier(imageOrg, imageId, imageTag);
    }
}