using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class UniqueGeneIdentifierTests
{
    [Fact]
    public void ToString_ReturnsCorrectResult()
    {
        var uniqueId = new UniqueGeneIdentifier(
            GeneType.Fodder,
            GeneIdentifier.New("gene:acme/acme-tools/1.0:test-fodder"),
            Architecture.New("hyperv/amd64"));

        uniqueId.ToString().Should().Be("fodder gene:acme/acme-tools/1.0:test-fodder (hyperv/amd64)");
    }
}
