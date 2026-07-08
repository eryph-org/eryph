using System.Linq;
using Eryph.Core.VmAgent;

namespace Eryph.Core.Tests;

public class StorageConfigMapperTests
{
    [Fact]
    public void Maps_defaults_datastores_and_environments_dropping_agent_local_settings()
    {
        var agent = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = @"C:\vms", Volumes = @"C:\vol", WatchFileSystem = false,
            },
            Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"D:\fast" }],
            Environments =
            [
                new VmHostAgentEnvironmentConfiguration
                {
                    Name = "prod",
                    Defaults = new VmHostAgentDefaultsConfiguration { Vms = @"E:\prod\vms" },
                    Datastores = [new VmHostAgentDataStoreConfiguration { Name = "fast", Path = @"E:\prod\fast" }],
                },
            ],
            Ovn = new VmHostAgentOvnConfiguration(),
        };

        var storage = StorageConfigMapper.FromVmHostAgentConfiguration(agent);

        storage.Defaults!.Vms.Should().Be(@"C:\vms");
        storage.Defaults.Volumes.Should().Be(@"C:\vol");
        storage.Datastores.Should().ContainSingle().Which.Path.Should().Be(@"D:\fast");
        var prod = storage.Environments.Should().ContainSingle().Subject;
        prod.Name.Should().Be("prod");
        prod.Defaults!.Vms.Should().Be(@"E:\prod\vms");
        prod.Datastores.Single().Path.Should().Be(@"E:\prod\fast");
        // StorageConfig has no place for WatchFileSystem/OVN — they are agent-local and not distributed.
    }

    [Fact]
    public void Defaults_with_no_paths_map_to_null()
    {
        var agent = new VmHostAgentConfiguration
        {
            Defaults = new VmHostAgentDefaultsConfiguration { WatchFileSystem = true }, // no paths
        };

        var storage = StorageConfigMapper.FromVmHostAgentConfiguration(agent);

        storage.Defaults.Should().BeNull();
        storage.Datastores.Should().BeEmpty();
        storage.Environments.Should().BeEmpty();
    }
}
