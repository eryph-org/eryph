using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;
using Moq;

using static LanguageExt.Prelude;

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

    public static void SetupCatletGene(
        this Mock<IGenePoolReader> mock,
        string geneSetIdentifier,
        CatletConfig catletConfig)
    {
        var validGeneSetId = GeneSetIdentifier.New(geneSetIdentifier);
        var validGeneId = new GeneIdentifier(validGeneSetId, GeneName.New("catlet"));
        var json = CatletConfigJsonSerializer.Serialize(catletConfig);

        mock.Setup(m => m.GetGeneContent(
                new UniqueGeneIdentifier(GeneType.Catlet, validGeneId, Architecture.New("any")),
                It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, string>(json));
    }

    public static void SetupFodderGene(
        this Mock<IGenePoolReader> mock,
        string geneIdentifier,
        string architecture,
        FodderGeneConfig fodderGene)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var json = FodderGeneConfigJsonSerializer.Serialize(fodderGene);

        mock.Setup(m => m.GetGeneContent(
                new UniqueGeneIdentifier(GeneType.Fodder, validGeneId, Architecture.New(architecture)),
                It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, string>(json));
    }
}
