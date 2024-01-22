using System;
using System.Security.Cryptography;
using System.Text.Json;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Genetics;

public abstract class GenePoolBase
{
    protected static Either<Error, (string HashAlg, string Hash)> ParseGeneHash(string geneHash)
    {
        var geneParts = geneHash.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (geneParts.Length != 2 || geneParts[0] != "sha256")
            return Error.New($"Invalid gene hash '{geneHash}'");

        return (geneParts[0], geneParts[1].ToLowerInvariant());
    }

    protected static Either<Error, (string HashAlg, string Hash)> ParseGenePartHash(string partHash)
    {
        var geneParts = partHash.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (geneParts.Length != 2 || geneParts[0] != "sha1")
            return Error.New($"Invalid gene part hash '{partHash}'");

        return (geneParts[0], geneParts[1].ToLowerInvariant());
    }

    protected static GenesetTagManifestData ReadGeneSetManifest(string json)
    {
        return JsonSerializer.Deserialize<GenesetTagManifestData>(json);
    }

    protected static GeneManifestData ReadGeneManifest(string json)
    {
        return JsonSerializer.Deserialize<GeneManifestData>(json);
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