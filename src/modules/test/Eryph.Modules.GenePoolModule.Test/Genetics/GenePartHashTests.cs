using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class GenePartHashTests
{
    [Theory]
    [InlineData("sha1:78bfd33a129aba75b45f3171ffb198a283e66ae5")]
    [InlineData("SHA1:78BFD33A129ABA75B45F3171FFB198A283E66AE5")]
    public void NewValidation_ValidHash_ReturnsValue(string input)
    {
        var genePartHash = GenePartHash.NewValidation(input)
            .Should().BeSuccess().Subject;

        genePartHash.Value.Should().Be(input.ToLowerInvariant());
        genePartHash.Algorithm.Should().Be("sha1");
        genePartHash.Hash.Value.Should().Be("78bfd33a129aba75b45f3171ffb198a283e66ae5");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b:c")]
    // A gene hash is also not a valid gene part hash
    [InlineData("sha256:185590ebc0e0e2d3d86e4896fba24ef5331ed021b79fe29d240d0ab356758426")]
    public void NewValidation_InvalidHash_ReturnsError(string input)
    {
        GenePartHash.NewValidation(input).Should().BeFail();
    }
}
