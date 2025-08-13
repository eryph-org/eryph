using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.GenePool.Genetics;

internal static class HashAlgorithmExtensions
{
    public static HashAlgorithm CreateAlgorithm(this GeneHash geneHash)
    {
        if (geneHash.Algorithm is not "sha256")
            throw new ArgumentException(
                $"The algorithm {geneHash.Algorithm} of the gene hash is not supported.",
                nameof(geneHash));

        return SHA256.Create();
    }

    public static  HashAlgorithm CreateAlgorithm(this GenePartHash genePartHash)
    {
        if (genePartHash.Algorithm is not "sha1")
            throw new ArgumentException(
                $"The algorithm {genePartHash.Algorithm} of the gene part hash is not supported.",
                nameof(genePartHash));

        return SHA1.Create();
    }

    public static GeneHash ToGeneHash(this HashAlgorithm hashAlgorithm)
    {
        if (hashAlgorithm is not SHA256 sha256)
            throw new ArgumentException(
                $"The algorithm {hashAlgorithm.GetType().Name} is not supported.",
                nameof(hashAlgorithm));

        if (sha256.Hash is null)
            throw new HashVerificationException(
                $"The hash of the gene has not been computed.");

        return new GeneHash($"sha256:{Convert.ToHexString(sha256.Hash).ToLowerInvariant()}");
    }

    public static GenePartHash ToGenePartHash(this HashAlgorithm hashAlgorithm)
    {
        if (hashAlgorithm is not SHA1 sha1)
            throw new ArgumentException(
                $"The algorithm {hashAlgorithm.GetType().Name} is not supported.",
                nameof(hashAlgorithm));

        if (sha1.Hash is null)
            throw new HashVerificationException(
                $"The hash of the gene part has not been computed.");
        return new GenePartHash($"sha1:{Convert.ToHexString(sha1.Hash).ToLowerInvariant()}");
    }
}
