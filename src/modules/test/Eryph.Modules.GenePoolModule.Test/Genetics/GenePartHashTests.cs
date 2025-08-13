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
    [InlineData("sha1:afa6c8b3a2fae95785dc7d9685a57835d703ac88")]
    [InlineData("SHA1:AFA6C8B3A2FAE95785DC7D9685A57835D703AC88")]
    public void NewValidation_ValidHash_ReturnsValue(string input)
    {
        var genePartHash = GenePartHash.NewValidation(input)
            .Should().BeSuccess().Subject;

        genePartHash.Value.Should().Be(input.ToLowerInvariant());
        genePartHash.Algorithm.Should().Be("sha1");
        genePartHash.Hash.Should().Be("afa6c8b3a2fae95785dc7d9685a57835d703ac88");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b:c")]
    // A gene hash is also not a valid gene part hash
    [InlineData("sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c")]
    public void NewValidation_InvalidHash_ReturnsError(string input)
    {
        GenePartHash.NewValidation(input).Should().BeFail();
    }
}
