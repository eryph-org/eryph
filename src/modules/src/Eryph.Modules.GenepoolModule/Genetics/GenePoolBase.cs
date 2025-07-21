using System;
using System.Security.Cryptography;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Genepool.Genetics;

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

    protected static HashAlgorithm CreateHashAlgorithm(string name)
    {
        return name == "sha1" ? SHA1.Create() : SHA256.Create();
    }

    protected static string GetHashString(byte[]? hashBytes)
    {
        return 
            hashBytes == null 
                ? string.Empty 
                : BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}