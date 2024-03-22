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

    [Fact]
    public void ValidateVmHostAgentConfig_InvalidData_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
            Defaults = new()
            {
                Vms = "not|a|path",
            },
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "invalid store",
                    Path = "not|a|path",
                },
            },
            Environments = new[]
            {
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "invalid env",
                    Defaults = new()
                    {
                        Vms = "not|a|path",
                        Volumes = @"z:\envs\env1\volumes",

                    },
                    Datastores = new[]
                    {
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "invalid env store",
                            Path = "not|a|path",
                        },
                    },
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        var issues = result.Should().BeFail().Which.ToList();

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Defaults.Vms");
                issue.Message.Should().Be("The value must be a valid Windows path but contains invalid characters.");
            },
            issue =>
            {
                issue.Member.Should().Be("Datastores[0].Name");
                issue.Message.Should().Be("The data store name contains invalid characters. Only latin characters, numbers, dots and hyphens are permitted.");
            },
            issue =>
            {
                issue.Member.Should().Be("Datastores[0].Path");
                issue.Message.Should().Be("The value must be a valid Windows path but contains invalid characters.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Name");
                issue.Message.Should().Be("The environment name contains invalid characters. Only latin characters, numbers, dots and hyphens are permitted.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Defaults.Vms");
                issue.Message.Should().Be("The value must be a valid Windows path but contains invalid characters.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Datastores[0].Name");
                issue.Message.Should().Be("The data store name contains invalid characters. Only latin characters, numbers, dots and hyphens are permitted.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Datastores[0].Path");
                issue.Message.Should().Be("The value must be a valid Windows path but contains invalid characters.");
            });
    }

    [Fact]
    public void ValidateVmHostAgentConfig_DuplicatePaths_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
            Defaults = new()
            {
                Vms = @"z:\default",
                Volumes = @"z:\default",
            },
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store1",
                    Path = @"z:\stores\store1",
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store2",
                    Path = @"z:\stores\store2",
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
                        Volumes = @"z:\stores\store2",
                    },
                    Datastores = new[]
                    {
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "store1",
                            Path = @"z:\stores\store1",
                        },
                    },
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().BeEmpty();
                issue.Message.Should().Be(@"The path 'z:\stores\store2' is not unique.");
            },
            issue =>
            {
                issue.Member.Should().BeEmpty();
                issue.Message.Should().Be(@"The path 'z:\stores\store1' is not unique.");
            },
            issue =>
            {
                issue.Member.Should().BeEmpty();
                issue.Message.Should().Be(@"The path 'z:\default' is not unique.");
            });
    }

    [Fact]
    public void ValidateVmHostAgentConfig_DuplicatePathsInDifferentRepresentations_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store1",
                    Path = @"z:\stores\store",
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store2",
                    Path = @"z:\stores\store\",
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store3",
                    Path = @"Z:\stores\store2",
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store4",
                    Path = @"Z:\STORES\STORE2",
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().BeEmpty();
                issue.Message.Should().Be(@"The path 'z:\stores\store' is not unique.");
            },
            issue =>
            {
                issue.Member.Should().BeEmpty();
                issue.Message.Should().Be(@"The path 'z:\stores\store2' is not unique.");
            });
    }

    [Fact]
    public void ValidateVmHostAgentConfig_DuplicateNames_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store1",
                    Path = @"z:\stores\store1",
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "Store1",
                    Path = @"z:\stores\store2",
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
                },
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "Env1",
                    Defaults = new()
                    {
                        Vms = @"z:\envs\env2\vms",
                        Volumes = @"z:\envs\env2\volumes",
                    },
                }
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Datastores");
                issue.Message.Should().Be("The data store 'store1' is not unique.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments");
                issue.Message.Should().Be("The environment 'env1' is not unique.");
            });
    }

    [Fact]
    public void ValidateVmHostAgentConfig_DuplicateNamesInEnvironment_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
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
                            Path = @"z:\stores\store1",
                        },
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "Store1",
                            Path = @"z:\stores\store2",
                        },
                    },
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Datastores");
                issue.Message.Should().Be("The data store 'store1' is not unique.");
            });
    }

    [Fact]
    public void ValidateVmHostAgentConfig_EnvironmentWithEmptyDefaults_ReturnsFail()
    {
        var config = new VmHostAgentConfiguration()
        {
            Environments = new[]
            {
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "env1",
                    Defaults = new(),
                },
            },
        };

        var result = ValidateVmHostAgentConfig(config);

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Defaults.Vms");
                issue.Message.Should().Be("The Vms is required.");
            },
            issue =>
            {
                issue.Member.Should().Be("Environments[0].Defaults.Volumes");
                issue.Message.Should().Be("The Volumes is required.");
            });
    }
}
