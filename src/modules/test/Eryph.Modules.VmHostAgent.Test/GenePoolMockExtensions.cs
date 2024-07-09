using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using LanguageExt;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test;

public static class GenePoolMockExtensions
{
    public static void SetupGenes(
        this Mock<IGeneProvider> geneProviderMock,
        params (GeneType GeneType, GeneIdentifier GeneId)[] genes)
    {
        foreach (var gene in genes.ToSeq())
        {
            geneProviderMock.Setup(m => m.ProvideGene(
                    gene.GeneType,
                    gene.GeneId,
                    It.IsAny<Func<string, int, Task<Unit>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(RightAsync<Error, PrepareGeneResponse>(new PrepareGeneResponse()
                {
                    RequestedGene = new GeneIdentifierWithType(gene.GeneType, gene.GeneId),
                    ResolvedGene = new GeneIdentifierWithType(gene.GeneType, gene.GeneId),
                }));
        }
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

    public static void SetupGeneSets(
        this Mock<IGeneProvider> geneProviderMock,
        params (GeneSetIdentifier Source, GeneSetIdentifier Target)[] geneSets)
    {
        foreach (var mappedGeneSet in geneSets.ToSeq())
        {
            geneProviderMock.Setup(m => m.ResolveGeneSet(
                    mappedGeneSet.Source,
                    It.IsAny<CancellationToken>()))
                .Returns(RightAsync<Error, GeneSetIdentifier>(mappedGeneSet.Target));
        }
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
}