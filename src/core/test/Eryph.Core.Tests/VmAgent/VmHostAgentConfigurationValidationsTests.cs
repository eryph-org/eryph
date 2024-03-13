using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;

using static Eryph.Core.VmAgent.VmHostAgentConfigurationValidations;

namespace Eryph.Core.Tests.VmAgent;

public class VmHostAgentConfigurationValidationsTests
{
    [Fact]
    public void ValidateVmHostAgentConfig_EmptyConfig_ReturnsSuccess()
    {
        var config = new VmHostAgentConfiguration();

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeSuccess();
    }

    [Fact]
    public void ValidateVmHostAgentConfig_ValidConfig_ReturnsSuccess()
    {
        var config = new VmHostAgentConfiguration()
        {
            Defaults = new()
            {
                Vms = @"z:\default\vms",
                Volumes = @"z:\default\volumes",
            },
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store1",
                    Path = @"z:\stores\store1",
                },
            },
            Environments = new[]
            {
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "env1",
                    Defaults = new()
                    {
                        Vms = @"z:\envs\env1\vms",
                        Volumes = @"z:\envs\env1\volumes",
                    },
                    Datastores = new[]
                    {

                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "store1",
                            Path = @"z:\envs\env1\store1",
                        },
                    },
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeSuccess();
    }
}
