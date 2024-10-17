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

public static class LocalGenepoolReaderMockExtensions
{
    public static void SetupGenesetReferences(
        this Mock<ILocalGenepoolReader> mock,
        params (string Source, string Target)[] references)
    {
        var map = references.ToSeq()
            .Map(r => (GeneSetIdentifier.New(r.Source), GeneSetIdentifier.New(r.Target)))
            .ToHashMap();

        mock.Setup(m => m.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns((GeneSetIdentifier source) =>
                Right<Error, Option<GeneSetIdentifier>>(map.Find(source)));
    }

    public static void SetupCatletGene(
        this Mock<ILocalGenepoolReader> mock,
        string geneSetIdentifier,
        CatletConfig catletConfig)
    {
        var validGeneSetId = GeneSetIdentifier.New(geneSetIdentifier);
        var validGeneId = new GeneIdentifier(validGeneSetId, GeneName.New("catlet"));
        var json = ConfigModelJsonSerializer.Serialize(catletConfig);

        mock.Setup(m => m.ReadGeneContent(GeneType.Catlet, GeneArchitecture.New("any"),validGeneId))
            .Returns(RightAsync<Error, string>(json));
    }

    public static void SetupFodderGene(
        this Mock<ILocalGenepoolReader> mock,
        string geneIdentifier,
        FodderGeneConfig fodderGene)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var json = ConfigModelJsonSerializer.Serialize(fodderGene);

        mock.Setup(m => m.ReadGeneContent(GeneType.Fodder, GeneArchitecture.New("any"), validGeneId))
            .Returns(RightAsync<Error, string>(json));
    }
}
