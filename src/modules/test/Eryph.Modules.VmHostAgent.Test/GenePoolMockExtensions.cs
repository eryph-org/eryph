using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using LanguageExt;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;
using Array = System.Array;

namespace Eryph.Modules.VmHostAgent.Test;

public static class GenePoolMockExtensions
{
    public static void SetupGenes(
        this Mock<IGeneProvider> geneProviderMock,
        params (GeneType GeneType, GeneIdentifier GeneId)[] genes)
    {
        var map = genes.ToHashSet();
        
        geneProviderMock.Setup(m => m.ProvideGene(
                It.IsAny<GeneType>(),
                It.IsAny<GeneIdentifier>(),
                It.IsAny<Func<string, int, Task<Unit>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((GeneType geneType, GeneIdentifier geneId, Func<string, int, Task<Unit>>_, CancellationToken _) => 
                map.Contains((geneType, geneId))
                    ? RightAsync<Error, PrepareGeneResponse>(new PrepareGeneResponse()
                    {
                        RequestedGene = new GeneIdentifierWithType(geneType, geneId),
                    })
                    : LeftAsync<Error, PrepareGeneResponse>(Error.New("The gene was not found.")));
    }

    public static void SetupGenes(
        this Mock<IGeneProvider> geneProviderMock,
        params (GeneType GeneType, string GeneId)[] genes)
    {
        var mapped = genes.ToSeq()
            .Map(g => (g.GeneType, GeneIdentifier.New(g.GeneId)))
            .ToArray();

        geneProviderMock.SetupGenes(mapped);
    }

    public static void SetupGenes(
        this Mock<IGeneProvider> geneProviderMock)
    {
        geneProviderMock.SetupGenes(Array.Empty<(GeneType, GeneIdentifier)>());
    }

    public static void SetupGeneSets(
        this Mock<IGeneProvider> geneProviderMock,
        params (GeneSetIdentifier Source, GeneSetIdentifier Target)[] geneSets)
    {
        var map = geneSets.ToHashMap();

            geneProviderMock.Setup(m => m.ResolveGeneSet(
                    It.IsAny<GeneSetIdentifier>(),
                    It.IsAny<CancellationToken>()))
                .Returns((GeneSetIdentifier geneSetId, CancellationToken _) =>
                    map.Find(geneSetId).ToEither(Error.New("The gene set was not found.")).ToAsync());
    }

    public static void SetupGeneSets(
        this Mock<IGeneProvider> geneProviderMock,
        params (string Source, string Target)[] geneSets)
    {
        var mapped = geneSets.ToSeq()
            .Map(t => (GeneSetIdentifier.New(t.Source), GeneSetIdentifier.New(t.Target)))
            .ToArray();

        geneProviderMock.SetupGeneSets(mapped);
    }

    public static void SetupGeneSets(
        this Mock<IGeneProvider> geneProviderMock)
    {
        geneProviderMock.SetupGeneSets(Array.Empty<(GeneSetIdentifier, GeneSetIdentifier)>());
    }
}