﻿using Eryph.ConfigModel;
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
                RightAsync<Error, Option<GeneSetIdentifier>>(map.Find(source)));
    }

    public static void SetupCatletGene(
        this Mock<ILocalGenepoolReader> mock,
        string geneSetIdentifier,
        CatletConfig catletConfig)
    {
        var validGeneSetId = GeneSetIdentifier.New(geneSetIdentifier);
        var validGeneId = new GeneIdentifier(validGeneSetId, GeneName.New("catlet"));
        var json = CatletConfigJsonSerializer.Serialize(catletConfig);

        mock.Setup(m => m.ReadGeneContent(new UniqueGeneIdentifier(
                GeneType.Catlet, validGeneId, Architecture.New("any"))))
            .Returns(RightAsync<Error, string>(json));
    }

    public static void SetupFodderGene(
        this Mock<ILocalGenepoolReader> mock,
        string geneIdentifier,
        string architecture,
        FodderGeneConfig fodderGene)
    {
        var validGeneId = GeneIdentifier.New(geneIdentifier);
        var json = FodderGeneConfigJsonSerializer.Serialize(fodderGene);

        mock.Setup(m => m.ReadGeneContent(new UniqueGeneIdentifier(
                GeneType.Fodder, validGeneId, Architecture.New(architecture))))
            .Returns(RightAsync<Error, string>(json));
    }
}
