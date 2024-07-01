using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.GenePool.Model;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.SomeHelp;
using Moq;
using Xunit;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class CatletBreedingTests
{
    private readonly Mock<ILocalGenepoolReader> _genepoolReaderMock = new();

    [Fact]
    public void TEst()
    {
        var config = new CatletConfig
        {
            Drives =
            [
                new CatletDriveConfig
                {
                    Source = "gene:acme/test-os/latest:sda",
                }
            ],
            Fodder =
            [
                new FodderConfig()
                {
                    Source = "gene:acme/test-tools/latest:test-fodder",
                }
            ],
        };

        _genepoolReaderMock.SetupGenesetReference("acme/test-os/latest", "acme/test-os/1.0");
        _genepoolReaderMock.SetupGenesetReference("acme/test-os/1.0", None);
        _genepoolReaderMock.SetupGenesetReference("acme/test-tools/latest", "acme/test-tools/1.0");
        _genepoolReaderMock.SetupGenesetReference("acme/test-tools/1.0", None);

        var result = CatletBreeding.ResolveGenesetIdentifiers(config, _genepoolReaderMock.Object);

        var resultConfig = result.Should().BeRight().Subject;
        resultConfig.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/test-os/1.0:sda"));
        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Source.Should().Be("gene:acme/test-tools/1.0:test-fodder"));
    }
}

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
            .Returns(Right<Error,string>(json));
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