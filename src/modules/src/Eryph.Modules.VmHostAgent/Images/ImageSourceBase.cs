using System;
using System.Text.Json;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public abstract class ImageSourceBase
{
    protected Either<PowershellFailure, string> ParseArtifactName(string artifact)
    {
        var artifactParts = artifact.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (artifactParts.Length != 3 || artifactParts[0] != "zip" || artifactParts[1] != "sha256")
            return new PowershellFailure { Message = $"Invalid artifact name '{artifact}'" };

        return artifactParts[2];
    }

    protected ManifestData ReadManifest(string json)
    {
        return JsonSerializer.Deserialize<ManifestData>(json, new JsonSerializerOptions{IncludeFields = true});
    }
}