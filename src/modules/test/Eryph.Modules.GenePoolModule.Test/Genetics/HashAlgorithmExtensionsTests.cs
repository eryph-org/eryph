using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class HashAlgorithmExtensionsTests
{
    private const string PlainText = "This is a test.";
    private const string Sha1Hash = "afa6c8b3a2fae95785dc7d9685a57835d703ac88";
    private const string Sha256Hash = "a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c";

    [Fact]
    public void CreateAlgorithm_GeneHash_ReturnsFreshSha256()
    {
        var geneHash = GeneHash.New($"sha256:{Sha256Hash}");
        
        using var sha256 = geneHash.CreateAlgorithm();

        sha256.Should().BeAssignableTo<SHA256>();
        sha256.Hash.Should().BeNull();
    }

    [Fact]
    public void CreateAlgorithm_GenePartHash_ReturnsFreshSha1()
    {
        var genePartHash = GenePartHash.New($"sha1:{Sha1Hash}");

        using var sha1 = genePartHash.CreateAlgorithm();

        sha1.Should().BeAssignableTo<SHA1>();
        sha1.Hash.Should().BeNull();
    }

    [Fact]
    public void ToGenePartHash_Sha1Algorithm_ReturnsGenePartHash()
    {
        using var sha1 = SHA1.Create();
        WriteText(sha1, PlainText);

        var geneHash = sha1.ToGenePartHash();

        geneHash.Algorithm.Should().Be("sha1");
        geneHash.Hash.Should().Be(Sha1Hash);
    }

    [Fact]
    public void ToGeneHash_Sha256Algorithm_ReturnsGeneHash()
    {
        using var sha256 = SHA256.Create();
        WriteText(sha256, PlainText);
        
        var geneHash = sha256.ToGeneHash();
        
        geneHash.Algorithm.Should().Be("sha256");
        geneHash.Hash.Should().Be(Sha256Hash);
    }

    private void WriteText(HashAlgorithm hashAlgorithm, string text)
    {
        // Use a CryptoStream as we need a HashAlgorithm in a
        // finalized state for the tests. Methods like ComputeHash
        // immediately reset the HashAlgorithm instance.
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, hashAlgorithm, CryptoStreamMode.Write);
        cryptoStream.Write(Encoding.UTF8.GetBytes(text).AsSpan());
        cryptoStream.FlushFinalBlock();
    }
}
