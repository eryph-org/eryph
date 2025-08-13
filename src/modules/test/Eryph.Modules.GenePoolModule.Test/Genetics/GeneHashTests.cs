using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Modules.GenePoolModule.Test.Genetics;

public class GeneHashTests
{
    [Theory]
    [InlineData("sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c")]
    [InlineData("SHA256:A8A2F6EBE286697C527EB35A58B5539532E9B3AE3B64D4EB0A46FB657B41562C")]
    public void NewValidation_ValidHash_ReturnsValue(string input)
    {
        var genePartHash = GeneHash.NewValidation(input)
            .Should().BeSuccess().Subject;

        genePartHash.Value.Should().Be(input.ToLowerInvariant());
        genePartHash.Algorithm.Should().Be("sha256");
        genePartHash.Hash.Should().Be("a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c");     
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b:c")]
    // A gene part hash is also not a valid gene hash
    [InlineData("sha1:afa6c8b3a2fae95785dc7d9685a57835d703ac88")]
    public void NewValidation_InvalidHash_ReturnsError(string input)
    {
        GeneHash.NewValidation(input).Should().BeFail();
    }
}
