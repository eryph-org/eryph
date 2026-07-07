using Eryph.Core.VmAgent;

namespace Eryph.Core.Tests;

public class StorageConfigValidationTests
{
    [Fact]
    public void Default_datastore_and_environment_are_always_allowed()
    {
        var distributed = new StorageConfig();

        StorageConfigValidation.IsDataStoreAllowed(distributed, "default").Should().BeTrue();
        StorageConfigValidation.IsEnvironmentAllowed(distributed, "default").Should().BeTrue();
    }

    [Theory]
    [InlineData("fast", true)]
    [InlineData("FAST", true)] // case-insensitive
    [InlineData("slow", false)]
    public void Datastore_is_allowed_only_when_in_the_distributed_vocabulary(string name, bool expected)
    {
        var distributed = new StorageConfig { Datastores = ["fast"], Environments = [] };

        StorageConfigValidation.IsDataStoreAllowed(distributed, name).Should().Be(expected);
    }

    [Theory]
    [InlineData("staging", true)]
    [InlineData("prod", false)]
    public void Environment_is_allowed_only_when_in_the_distributed_vocabulary(string name, bool expected)
    {
        var distributed = new StorageConfig { Datastores = [], Environments = ["staging"] };

        StorageConfigValidation.IsEnvironmentAllowed(distributed, name).Should().Be(expected);
    }

    [Fact]
    public void Unused_local_datastores_excludes_default_and_distributed_names()
    {
        var distributed = new StorageConfig { Datastores = ["fast"], Environments = [] };
        var local = new VmHostAgentConfiguration
        {
            Datastores =
            [
                new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"D:\fast" }, // distributed → used
                new VmHostAgentDataStoreConfiguration { Name = "slow", Path = @"D:\slow" }, // not distributed → unused
            ],
        };

        StorageConfigValidation.GetUnusedLocalDatastores(distributed, local)
            .Should().BeEquivalentTo("slow");
    }

    [Fact]
    public void Unused_local_environments_lists_names_not_in_the_distributed_vocabulary()
    {
        var distributed = new StorageConfig { Datastores = [], Environments = ["staging"] };
        var local = new VmHostAgentConfiguration
        {
            Environments =
            [
                new VmHostAgentEnvironmentConfiguration { Name = "staging" }, // distributed → used
                new VmHostAgentEnvironmentConfiguration { Name = "prod" }, // not distributed → unused
            ],
        };

        StorageConfigValidation.GetUnusedLocalEnvironments(distributed, local)
            .Should().BeEquivalentTo("prod");
    }

    [Fact]
    public void Unused_local_names_are_empty_when_no_local_config()
    {
        var distributed = new StorageConfig { Datastores = ["fast"], Environments = ["staging"] };
        var local = new VmHostAgentConfiguration();

        StorageConfigValidation.GetUnusedLocalDatastores(distributed, local).Should().BeEmpty();
        StorageConfigValidation.GetUnusedLocalEnvironments(distributed, local).Should().BeEmpty();
    }
}
