using System.Linq;
using Eryph.Core.VmAgent;

namespace Eryph.Core.Tests;

public class StorageConfigMergeTests
{
    [Fact]
    public void Distributed_default_paths_override_local_but_preserve_watch_flag()
    {
        var local = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = @"C:\local\vms", Volumes = @"C:\local\volumes", WatchFileSystem = false,
            },
        };
        var distributed = new StorageConfig
        {
            Defaults = new StorageDefaultsConfig { Vms = @"D:\dist\vms" }, // Volumes not set → keep local
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        merged.Defaults.Vms.Should().Be(@"D:\dist\vms");
        merged.Defaults.Volumes.Should().Be(@"C:\local\volumes"); // preserved (distributed left it null)
        merged.Defaults.WatchFileSystem.Should().BeFalse(); // local-only setting preserved
    }

    [Fact]
    public void Null_distributed_defaults_keep_local_defaults()
    {
        var local = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration { Vms = @"C:\vms", Volumes = @"C:\vol" },
        };

        var merged = StorageConfigMerge.Apply(local, new StorageConfig());

        merged.Defaults.Vms.Should().Be(@"C:\vms");
        merged.Defaults.Volumes.Should().Be(@"C:\vol");
    }

    [Fact]
    public void Distributed_datastore_path_overrides_the_local_datastore_of_the_same_name()
    {
        var local = new VmHostAgentConfiguration
        {
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"C:\old", WatchFileSystem = false }],
        };
        var distributed = new StorageConfig
        {
            Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\new" }],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        var fast = merged.Datastores!.Single(d => d.Name == "fast");
        fast.Path.Should().Be(@"D:\new");
        fast.WatchFileSystem.Should().BeFalse(); // preserved from the local entry
    }

    [Fact]
    public void Distributed_datastore_is_added_when_absent_locally()
    {
        var local = new VmHostAgentConfiguration();
        var distributed = new StorageConfig
        {
            Datastores = [new StorageDatastoreConfig { Name = "new", Path = @"D:\new" }],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        merged.Datastores!.Single(d => d.Name == "new").Path.Should().Be(@"D:\new");
    }

    [Fact]
    public void Path_less_distributed_datastore_leaves_the_local_mapping_untouched()
    {
        var local = new VmHostAgentConfiguration
        {
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"C:\keep" }],
        };
        var distributed = new StorageConfig
        {
            // vocabulary only — no path to contribute
            Datastores = [new StorageDatastoreConfig { Name = "fast" }],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        merged.Datastores!.Single(d => d.Name == "fast").Path.Should().Be(@"C:\keep");
    }

    [Fact]
    public void Local_datastore_not_in_distributed_is_kept()
    {
        var local = new VmHostAgentConfiguration
        {
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "local-only", Path = @"C:\keep" }],
        };

        var merged = StorageConfigMerge.Apply(local, new StorageConfig());

        merged.Datastores!.Single().Name.Should().Be("local-only");
    }

    [Fact]
    public void Environment_paths_are_merged_recursively_and_local_environments_are_kept()
    {
        var local = new VmHostAgentConfiguration
        {
            Environments =
            [
                new VmHostAgentEnvironmentConfiguration
                {
                    Name = "staging",
                    Defaults = new VmHostAgentDefaultsConfiguration { Vms = @"C:\stg\vms", Volumes = @"C:\stg\vol" },
                    Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"C:\stg\old" }],
                },
                new VmHostAgentEnvironmentConfiguration { Name = "local-only-env" },
            ],
        };
        var distributed = new StorageConfig
        {
            Environments =
            [
                new StorageEnvironmentConfig
                {
                    Name = "staging",
                    Defaults = new StorageDefaultsConfig { Vms = @"D:\stg\vms" }, // override vms only
                    Datastores = [new StorageDatastoreConfig { Name = "fast", Path = @"D:\stg\new" }],
                },
            ],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        var staging = merged.Environments!.Single(e => e.Name == "staging");
        staging.Defaults.Vms.Should().Be(@"D:\stg\vms"); // overridden
        staging.Defaults.Volumes.Should().Be(@"C:\stg\vol"); // preserved
        staging.Datastores.Should().ContainSingle(d => d.Name == "fast")
            .Which.Path.Should().Be(@"D:\stg\new");
        merged.Environments!.Should().Contain(e => e.Name == "local-only-env"); // kept
    }

    [Fact]
    public void Distributed_casing_is_adopted_for_a_matched_local_datastore()
    {
        // Path resolution downstream is case-sensitive, so the merged local name must match the
        // distributed (canonical) casing even when the distributed entry only supplies vocabulary.
        var local = new VmHostAgentConfiguration
        {
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "Fast", Path = @"C:\fast" }],
        };
        var distributed = new StorageConfig
        {
            Datastores = [new StorageDatastoreConfig { Name = "fast" }], // vocabulary only, different casing
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        var datastore = merged.Datastores!.Should().ContainSingle().Subject;
        datastore.Name.Should().Be("fast"); // canonical casing adopted
        datastore.Path.Should().Be(@"C:\fast"); // local path preserved
    }

    [Fact]
    public void Duplicate_distributed_datastore_names_collapse_to_the_last()
    {
        var distributed = new StorageConfig
        {
            Datastores =
            [
                new StorageDatastoreConfig { Name = "fast", Path = @"D:\first" },
                new StorageDatastoreConfig { Name = "fast", Path = @"D:\second" },
            ],
        };

        var merged = StorageConfigMerge.Apply(new VmHostAgentConfiguration(), distributed);

        merged.Datastores!.Should().ContainSingle().Which.Path.Should().Be(@"D:\second");
    }

    [Fact]
    public void Vocabulary_only_distributed_environment_without_a_local_counterpart_is_not_materialized()
    {
        var distributed = new StorageConfig
        {
            // name only — no defaults paths, no datastore paths
            Environments = [new StorageEnvironmentConfig { Name = "edge" }],
        };

        var merged = StorageConfigMerge.Apply(new VmHostAgentConfiguration(), distributed);

        merged.Environments.Should().NotContain(e => e.Name == "edge");
    }

    [Fact]
    public void Vocabulary_only_distributed_environment_with_a_local_counterpart_is_kept()
    {
        var local = new VmHostAgentConfiguration
        {
            Environments = [new VmHostAgentEnvironmentConfiguration { Name = "edge" }],
        };
        var distributed = new StorageConfig
        {
            Environments = [new StorageEnvironmentConfig { Name = "edge" }],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        merged.Environments!.Should().Contain(e => e.Name == "edge");
    }

    [Fact]
    public void Rename_that_collides_on_path_drops_the_stale_local_name()
    {
        var local = new VmHostAgentConfiguration
        {
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"D:\d" }],
        };
        var distributed = new StorageConfig
        {
            // Same path, new name — a rename.
            Datastores = [new StorageDatastoreConfig { Name = "quick", Path = @"D:\d" }],
        };

        var merged = StorageConfigMerge.Apply(local, distributed);

        merged.Datastores!.Should().ContainSingle();
        merged.Datastores!.Should().Contain(d => d.Name == "quick" && d.Path == @"D:\d");
        merged.Datastores!.Should().NotContain(d => d.Name == "fast");
    }

    [Fact]
    public void Local_only_ovn_configuration_is_preserved()
    {
        var local = new VmHostAgentConfiguration { Ovn = new VmHostAgentOvnConfiguration() };

        var merged = StorageConfigMerge.Apply(local, new StorageConfig());

        merged.Ovn.Should().BeSameAs(local.Ovn);
    }
}
