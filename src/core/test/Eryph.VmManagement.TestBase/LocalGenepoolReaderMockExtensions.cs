using Eryph.ConfigModel;
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
        Dictionary<string, string> references)
    {
        var parsedReferences = references
            .ToDictionary(kv => GeneSetIdentifier.New(kv.Key), kv => GeneSetIdentifier.New(kv.Value));

        mock.Setup(m => m.GetGenesetReference(It.IsAny<GeneSetIdentifier>()))
            .Returns((GeneSetIdentifier source) =>
                parsedReferences.TryGetValue(source, out var target)
                    ? Right<Error, Option<GeneSetIdentifier>>(target)
                    : Right<Error, Option<GeneSetIdentifier>>(None));
    }

    public static void SetupFodderGene(
        this Mock<ILocalGenepoolReader> mock,
        string geneIdentifier,
        FodderGeneConfig fodderGene)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var json = ConfigModelJsonSerializer.Serialize(fodderGene);

        mock.Setup(m => m.ReadGeneContent(GeneType.Fodder, validGeneId))
            .Returns(Right<Error, string>(json));
    }

    public static void SetupGenesetReference(
        this Mock<ILocalGenepoolReader> mock,
        GeneSetIdentifier source,
        GeneSetIdentifier target) =>
        mock.Setup(m => m.GetGenesetReference(source))
            .Returns(Right<Error, Option<GeneSetIdentifier>>(target));

    public static void SetupGenesetReference(
        this Mock<ILocalGenepoolReader> mock,
        string source,
        Option<string> target) =>
        mock.Setup(m => m.GetGenesetReference(GeneSetIdentifier.New(source)))
            .Returns(Right<Error, Option<GeneSetIdentifier>>(
                target.Map(t => GeneSetIdentifier.New(t))));
}