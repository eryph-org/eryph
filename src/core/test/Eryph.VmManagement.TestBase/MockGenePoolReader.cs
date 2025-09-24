using System.Security.Cryptography;
using System.Text;
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

/// <summary>
/// Mock implementation of <see cref="IGenePoolReader"/>. This mock provides
/// helper methods to properly arrange gene sets and genes.
/// </summary>
/// <remarks>
/// This mock extends <see cref="Mock{T}"/> which allows for additional checks
/// like invocation counts.
/// </remarks>
public class MockGenePoolReader : Mock<IGenePoolReader>
{
    private HashMap<GeneSetIdentifier, Option<GeneSetIdentifier>> _geneSets = Empty;
    private HashMap<GeneSetIdentifier, HashMap<UniqueGeneIdentifier, GeneHash>> _genes = Empty;
    private HashMap<(UniqueGeneIdentifier, GeneHash), string> _geneContents = Empty;

    public MockGenePoolReader()
    {
        Setup(m => m.GetReferencedGeneSet(It.IsAny<GeneSetIdentifier>(), It.IsAny<CancellationToken>()))
            .Returns((GeneSetIdentifier id, CancellationToken _) => _geneSets.Find(id)
                .ToEitherAsync(Error.New($"MOCK: The gene set {id} does not exist.")));

        Setup(m => m.GetGenes(It.IsAny<GeneSetIdentifier>(), It.IsAny<CancellationToken>()))
            .Returns((GeneSetIdentifier id, CancellationToken _) => _genes.Find(id)
                .ToEitherAsync(Error.New($"MOCK: The gene set {id} does not exist.")));

        Setup(m => m.GetGeneContent(It.IsAny<UniqueGeneIdentifier>(), It.IsAny<GeneHash>(), It.IsAny<CancellationToken>()))
            .Returns((UniqueGeneIdentifier id, GeneHash hash, CancellationToken _) => _geneContents.Find((id, hash))
                .ToEitherAsync(Error.New($"MOCK: The gene {id} ({hash}) does not exist.")));
    }

    public HashMap<UniqueGeneIdentifier, GeneHash> ResolvedGenes =>
        _geneContents.Keys.Map(k => (k.Item1, k.Item2)).ToHashMap();

    public void SetupGeneSet(string source, Option<string> target)
    {
        _geneSets = _geneSets.Add(GeneSetIdentifier.New(source), target.Map(GeneSetIdentifier.New));
    }

    public void SetupCatletGene(
        string geneSetId,
        CatletConfig catletConfig)
    {
        var validGeneSetId = GeneSetIdentifier.New(geneSetId);
        var validGeneId = new GeneIdentifier(validGeneSetId, GeneName.New("catlet"));
        var uniqueGeneId = new UniqueGeneIdentifier(GeneType.Catlet, validGeneId, Architecture.New("any"));
        var hash = ComputeHash(new GeneManifestData
        {
            Name = validGeneId.GeneName.Value,
            Architecture = Architecture.New("any").Value,
        });
        var json = CatletConfigJsonSerializer.Serialize(catletConfig);

        _geneContents = _geneContents.Add((uniqueGeneId, hash), json);
        _genes = _genes.AddOrUpdate(
            validGeneSetId,
            Some: existing => existing.Add(uniqueGeneId, hash),
            None: () => HashMap((uniqueGeneId, hash)));
    }

    public void SetupFodderGene(
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


        _geneContents = _geneContents.Add((uniqueGeneId, hash), json);
        _genes = _genes.AddOrUpdate(
            validGeneId.GeneSet,
            Some: existing => existing.Add(uniqueGeneId, hash),
            None: () => HashMap((uniqueGeneId, hash)));
    }

    /// <summary>
    /// Arranges a volume gene. The <paramref name="content"/> is only used to compute
    /// a <see cref="GeneHash"/> in a stable way. The <paramref name="content"/>
    /// cannot be read with <see cref="IGenePoolReader.GetGeneContent(UniqueGeneIdentifier, GeneHash, CancellationToken)"/>.
    /// </summary>
    public void SetupVolumeGene(
        string geneIdentifier,
        string architecture,
        string content)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var validArchitecture = Architecture.New(architecture);
        var uniqueGeneId = new UniqueGeneIdentifier(GeneType.Volume, validGeneId, validArchitecture);
        var hash = ComputeHash(content);

        // Do not add to _geneContents as the content of volume genes should never be read.

        _genes = _genes.AddOrUpdate(
            validGeneId.GeneSet,
            Some: existing => existing.Add(uniqueGeneId, hash),
            None: () => HashMap((uniqueGeneId, hash)));
    }

    private static GeneHash ComputeHash(GeneManifestData manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, GeneModelDefaults.SerializerOptions);
        var hash = SHA256.HashData(bytes);

        return GeneHash.New($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}");
    }

    private static GeneHash ComputeHash(string content) =>
        GeneHash.New($"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()}");
}
