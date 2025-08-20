using System.Security.Cryptography;
using System.Text.Json;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;
using GeneType = Eryph.Core.Genetics.GeneType;

namespace Eryph.VmManagement.TestBase;

public static class GenePoolReaderMockExtensions
{
    public static void SetupGeneSets(
        this Mock<IGenePoolReader> mock,
        params (string Source, Option<string> Target)[] references)
    {
        var map = references.ToSeq()
            .Map(r => (GeneSetIdentifier.New(r.Source), r.Target.Map(t => GeneSetIdentifier.New(t))))
            .ToHashMap();

        mock.Setup(m => m.GetReferencedGeneSet(It.IsAny<GeneSetIdentifier>(), It.IsAny<CancellationToken>()))
            .Returns((GeneSetIdentifier source, CancellationToken _) =>
                map.Find(source).Match(
                    Some: RightAsync<Error, Option<GeneSetIdentifier>>,
                    None: () => Error.New($"MOCK: The gene set {source} does not exist.")));
    }

    public static (UniqueGeneIdentifier Id, GeneHash Hash) SetupCatletGene(
        this Mock<IGenePoolReader> mock,
        string geneSetIdentifier,
        CatletConfig catletConfig)
    {
        var validGeneSetId = GeneSetIdentifier.New(geneSetIdentifier);
        var validGeneId = new GeneIdentifier(validGeneSetId, GeneName.New("catlet"));
        var uniqueGeneId = new UniqueGeneIdentifier(GeneType.Catlet, validGeneId, Architecture.New("any"));
        var hash = ComputeHash(new GeneManifestData
        {
            Name = validGeneId.GeneName.Value,
            Architecture = Architecture.New("any").Value,
        });
        var json = CatletConfigJsonSerializer.Serialize(catletConfig);
        

        mock.Setup(m => m.GetGeneContent(uniqueGeneId, hash, It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, string>(json));

        return (uniqueGeneId, hash);
    }

    public static (UniqueGeneIdentifier Id, GeneHash Hash) SetupFodderGene(
        this Mock<IGenePoolReader> mock,
        string geneIdentifier,
        string architecture,
        FodderGeneConfig fodderGene)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var validArchitecture = Architecture.New(architecture);
        var uniqueGeneId = new UniqueGeneIdentifier(GeneType.Fodder, validGeneId, validArchitecture);
        var hash = ComputeHash(new GeneManifestData
        {
            Name = validGeneId.GeneName.Value,
            Architecture = validArchitecture.Value,
        });
        var json = FodderGeneConfigJsonSerializer.Serialize(fodderGene);

        mock.Setup(m => m.GetGeneContent(uniqueGeneId, hash, It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, string>(json));

        return (uniqueGeneId, hash);
    }

    private static GeneHash ComputeHash(GeneManifestData manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, GeneModelDefaults.SerializerOptions);
        var hash = SHA256.HashData(bytes);

        return GeneHash.New($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}");
    }
}
