using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
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
    private readonly Mock<ILocalGenepoolReader> genepoolReaderMock = new();

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

        var result = CatletBreeding.ResolveGenesetIdentifiers(config, genepoolReaderMock.Object);

        var resultConfig = result.Should().BeSuccess().Subject;
        resultConfig.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/test-os/1.0:sda"));
        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Source.Should().Be("gene:acme/test-tools/1.0:test-fodder"));
    }
}

public static class LocalGenepoolReaderMockExtensions
{
    public static void SetupGenesetReference(
        this Mock<ILocalGenepoolReader> mock,
        GeneSetIdentifier source,
        GeneSetIdentifier target) =>
        mock.Setup(m => m.GetGenesetReference(source))
            .Returns(Right<Error, Option<GeneSetIdentifier>>(target));
}