using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.VmManagement.TestBase;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test;

public class CatletGeneResolvingTests
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

        var result = CatletGeneResolving.ResolveGenesetIdentifiers(config, _genepoolReaderMock.Object);

        var resultConfig = result.Should().BeRight().Subject;
        resultConfig.Drives.Should().SatisfyRespectively(
            drive => drive.Source.Should().Be("gene:acme/test-os/1.0:sda"));
        resultConfig.Fodder.Should().SatisfyRespectively(
            fodder => fodder.Source.Should().Be("gene:acme/test-tools/1.0:test-fodder"));
    }
}
