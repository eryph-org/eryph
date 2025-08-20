using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using YamlDotNet.Core.Tokens;

namespace Eryph.Core.Tests.Genetics;

public class UniqueGeneIdentifierTests
{
    [Theory]
    [InlineData(
        "catlet::gene:acme/acme-catlets/1.0:catlet[any]",
        "catlet::gene:acme/acme-catlets/1.0:catlet[any]")]
    [InlineData(
        "catlet::gene:acme/acme-catlets:catlet[any/any]",
        "catlet::gene:acme/acme-catlets/latest:catlet[any]")]
    [InlineData(
        "fodder::gene:acme/acme-tools/1.0:test-fodder[hyperv/amd64]",
        "fodder::gene:acme/acme-tools/1.0:test-fodder[hyperv/amd64]")]
    [InlineData(
        "volume::gene:acme/acme-catlets/1.0:sda[hyperv/amd64]",
        "volume::gene:acme/acme-catlets/1.0:sda[hyperv/amd64]")]
    public void NewValidation_ValidData_ReturnsResult(string value, string expected)
    {
        var validation = UniqueGeneIdentifier.NewValidation(value);

        var result = validation.Should().BeSuccess().Which.Value.Should().Be(expected);
    }

    [Fact]
    public void NewValidation_ValidData_PopulatesProperties()
    {
        var validation = UniqueGeneIdentifier.NewValidation("fodder::gene:acme/acme-tools/1.0:test-fodder[hyperv/amd64]");
        
        var result = validation.Should().BeSuccess().Subject;
        result.GeneType.Should().Be(GeneType.Fodder);
        result.Id.Should().Be(GeneIdentifier.New("gene:acme/acme-tools/1.0:test-fodder"));
        result.Architecture.Should().Be(Architecture.New("hyperv/amd64"));
    }

    [Fact]
    public void UniqueGeneIdentifier_ValidData_ReturnsResult()
    {
        var geneType = GeneType.Fodder;
        var geneId = GeneIdentifier.New("gene:acme/acme-tools/1.0:test-fodder");
        var architecture = Architecture.New("hyperv/amd64");

        var uniqueId = new UniqueGeneIdentifier(geneType, geneId, architecture);

        uniqueId.Value.Should().Be("fodder::gene:acme/acme-tools/1.0:test-fodder[hyperv/amd64]");
        uniqueId.GeneType.Should().Be(geneType);
        uniqueId.Id.Should().Be(geneId);
        uniqueId.Architecture.Should().Be(architecture);
    }
}
