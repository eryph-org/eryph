using Eryph.Core.VmAgent;

namespace Eryph.Core.Tests;

public class StorageConfigValidationTests
{
    [Fact]
    public void Default_datastore_is_always_allowed()
    {
        var distributed = new StorageConfig();

        StorageConfigValidation.IsDataStoreAllowed(distributed, "default").Should().BeTrue();
    }

    [Theory]
    [InlineData("fast", true)]
    [InlineData("FAST", true)] // case-insensitive
    [InlineData("slow", false)]
    public void Datastore_is_allowed_only_when_in_the_distributed_vocabulary(string name, bool expected)
    {
        var distributed = new StorageConfig { Datastores = [new StorageDatastoreConfig { Name = "fast" }], Environments = [] };

        StorageConfigValidation.IsDataStoreAllowed(distributed, name).Should().Be(expected);
    }

    [Fact]
    public void Unused_local_datastores_excludes_default_and_distributed_names()
    {
        var distributed = new StorageConfig { Datastores = [new StorageDatastoreConfig { Name = "fast" }], Environments = [] };
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
    public void Unused_local_names_are_empty_when_no_local_config()
    {
        var distributed = new StorageConfig { Datastores = [new StorageDatastoreConfig { Name = "fast" }], Environments = [new StorageEnvironmentConfig { Name = "staging" }] };
        var local = new VmHostAgentConfiguration();

        StorageConfigValidation.GetUnusedLocalDatastores(distributed, local).Should().BeEmpty();
    }

    [Fact]
    public void Validate_accepts_a_well_formed_config()
    {
        var config = new StorageConfig
        {
            Defaults = new StorageDefaultsConfig { Vms = @"D:\vms", Volumes = @"D:\volumes" },
            Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\fast" }],
            Environments = [],
        };

        StorageConfigValidation.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_rejects_an_invalid_environment_name()
    {
        var config = new StorageConfig
        {
            Datastores = [],
            Environments = [new StorageEnvironmentConfig { Name = "bad env!" }],
        };

        StorageConfigValidation.Validate(config).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_rejects_duplicate_datastore_names_case_insensitively()
    {
        var config = new StorageConfig
        {
            Datastores =
            [
                new StorageDatastoreConfig { Name = "Fast", Path = @"D:\fast1" },
                new StorageDatastoreConfig { Name = "fast", Path = @"D:\fast2" },
            ],
            Environments = [],
        };

        StorageConfigValidation.Validate(config).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_rejects_a_duplicate_path_between_an_environments_defaults_and_a_datastore()
    {
        var config = new StorageConfig
        {
            Datastores = [],
            Environments =
            [
                new StorageEnvironmentConfig
                {
                    Name = "staging",
                    Defaults = new StorageDefaultsConfig { Vms = @"D:\shared" },
                    Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\shared" }],
                },
            ],
        };

        StorageConfigValidation.Validate(config).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(@"D:\x", true)]
    [InlineData(@"relative\x", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public void IsFullyQualifiedPath_checks_well_formedness_in_an_os_agnostic_way(string? path, bool expected)
    {
        StorageConfigValidation.IsFullyQualifiedPath(path).Should().Be(expected);
    }

    [Fact]
    public void Validate_rejects_a_datastore_named_default()
    {
        var config = new StorageConfig
        {
            Datastores = [new StorageDatastoreConfig { Name = "default", Path = @"D:\default" }],
            Environments = [],
        };

        StorageConfigValidation.Validate(config).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_rejects_an_environment_named_default()
    {
        var config = new StorageConfig
        {
            Datastores = [],
            Environments = [new StorageEnvironmentConfig { Name = "default" }],
        };

        StorageConfigValidation.Validate(config).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_rejects_an_environment_datastore_not_declared_at_the_top_level()
    {
        var config = new StorageConfig
        {
            Datastores = [],
            Environments =
            [
                new StorageEnvironmentConfig
                {
                    Name = "staging",
                    Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\fast" }],
                },
            ],
        };

        var errors = StorageConfigValidation.Validate(config);

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("not declared in the top-level"));
    }
}
