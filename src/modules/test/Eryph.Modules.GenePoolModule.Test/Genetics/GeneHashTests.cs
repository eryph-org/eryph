using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class GeneHashTests
{
    [Theory]
    [InlineData("sha256:185590ebc0e0e2d3d86e4896fba24ef5331ed021b79fe29d240d0ab356758426")]
    [InlineData("SHA256:185590EBC0E0E2D3D86E4896FBA24EF5331ED021B79FE29D240D0AB356758426")]
    public void NewValidation_ValidHash_ReturnsValue(string input)
    {
        var genePartHash = GeneHash.NewValidation(input)
            .Should().BeSuccess().Subject;

        genePartHash.Value.Should().Be(input.ToLowerInvariant());
        genePartHash.Algorithm.Should().Be("sha256");
        genePartHash.Hash.Value.Should().Be("185590ebc0e0e2d3d86e4896fba24ef5331ed021b79fe29d240d0ab356758426");     
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b:c")]
    // A gene part hash is also not a valid gene hash
    [InlineData("sha1:78bfd33a129aba75b45f3171ffb198a283e66ae5")]
    public void NewValidation_InvalidHash_ReturnsError(string input)
    {
        GeneHash.NewValidation(input).Should().BeFail();
    }
}
