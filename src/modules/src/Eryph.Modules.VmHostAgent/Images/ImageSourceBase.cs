using System;
using System.IO;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Text.Json;
using Eryph.VmManagement;
using LanguageExt;

namespace Eryph.Modules.VmHostAgent.Images;

public abstract class ImageSourceBase
{
    protected static Either<PowershellFailure, (string HashAlg, string Hash)> ParseArtifactName(string artifact)
    {
        var artifactParts = artifact.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (artifactParts.Length != 2 || artifactParts[0] != "sha256")
            return new PowershellFailure { Message = $"Invalid artifact name '{artifact}'" };

        return (artifactParts[0], artifactParts[1].ToLowerInvariant());
    }

    protected static Either<PowershellFailure, (string HashAlg, string Hash)> ParseArtifactPartName(string artifactPart)
    {
        var artifactParts = artifactPart.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (artifactParts.Length != 2 || artifactParts[0] != "sha1")
            return new PowershellFailure { Message = $"Invalid artifact part name '{artifactPart}'" };

        return (artifactParts[0], artifactParts[1].ToLowerInvariant());
    }

    protected static ImageManifestData ReadImageManifest(string json)
    {
        return JsonSerializer.Deserialize<ImageManifestData>(json);
    }

    protected static ArtifactManifestData ReadArtifactManifest(string json)
    {
        return JsonSerializer.Deserialize<ArtifactManifestData>(json);
    }

    protected static HashAlgorithm CreateHashAlgorithm(string name)
    {
        return name == "sha1" ? SHA1.Create() : SHA256.Create();
    }

    protected static string GetHashString(byte[] hashBytes)
    {
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}