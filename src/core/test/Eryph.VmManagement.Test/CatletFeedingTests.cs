using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using Eryph.Resources.Machines;
using FluentAssertions;
using Xunit;

namespace Eryph.VmManagement.Test;

public class CatletFeedingTests
{
    [Fact]
    public void FeedSystemVariables_SystemVariablesAreAppended()
    {
        var catletId = Guid.NewGuid();
        var vmId = Guid.NewGuid();

        var metadata = new CatletMetadata()
        {
            MachineId = catletId,
            VMId = vmId,
        };

        var config = new CatletConfig();

        var result = config.FeedSystemVariables(metadata);

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
                variable.Value.Should().Be(catletId.ToString());
                variable.Type.Should().Be(VariableType.String);
                variable.Required.Should().BeFalse();
                variable.Secret.Should().BeFalse();
            });
    }
}
