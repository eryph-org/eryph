using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;

namespace Eryph.Core.Tests.Genetics;

public class CatletSystemDataFeedingTests
{
    [Fact]
    public void FeedSystemVariables_SystemVariablesAreAppended()
    {
        var catletId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        var config = new CatletConfig();

        var result = CatletSystemDataFeeding.FeedSystemVariables(config, catletId, vmId);

        result.Variables.Should().SatisfyRespectively(
            variable =>
            {
                variable.Name.Should().Be(EryphConstants.SystemVariables.CatletId);
                variable.Value.Should().Be(catletId.ToString());
                variable.Type.Should().Be(VariableType.String);
                variable.Required.Should().BeFalse();
                variable.Secret.Should().BeFalse();
            },
            variable =>
            {
                variable.Name.Should().Be(EryphConstants.SystemVariables.VmId);
                variable.Value.Should().Be(vmId.ToString());
                variable.Type.Should().Be(VariableType.String);
                variable.Required.Should().BeFalse();
                variable.Secret.Should().BeFalse();
            });
    }
}
