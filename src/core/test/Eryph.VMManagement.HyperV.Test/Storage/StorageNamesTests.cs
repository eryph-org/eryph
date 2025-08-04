using Eryph.Core.VmAgent;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.VmManagement.HyperV.Test.Storage;

public class StorageNamesTests
{
    public class ComplexVmHostAgentConfiguration
    {
        private readonly VmHostAgentConfiguration _vmHostAgentConfiguration = new()
        {
            Defaults = new()
            {
                Volumes = @"x:\default\test\volumes\eryph",
                Vms = @"x:\default\test\vms\eryph",
            },
            Datastores = new[]
            {
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "cluster",
                    Path = @"x:\cluster\test"
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "scratch",
                    Path = @"x:\scratch\test"
                }
            },
            Environments = new[]
            {
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "prod",
                    Defaults = new()
                    {
                        Volumes = @"x:\prod\test\volumes",
                        Vms = @"x:\prod\test\vms",
                    },
                    Datastores = new[]
                    {
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "cluster",
                            Path = @"x:\prod\cluster\test"
                        },
                    }
                },
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "qa",
                    Defaults = new()
                    {
                        Volumes = @"x:\qa\test\volumes",
                        Vms = @"x:\qa\test\vms",
                    },
                    Datastores = new[]
                    {
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "cluster",
                            Path = @"x:\qa\cluster\test"
                        },
                    }
                }
            },
        };

        [Theory]
        [InlineData(@"x:\default\test\vms\eryph\a6rkklnznsow", "default", "default", "default", "a6rkklnznsow")]
        [InlineData(@"x:\default\test\vms\eryph\a6rkklnznsow\test.file", "default", "default", "default", "a6rkklnznsow")]
        [InlineData(@"x:\default\test\vms\eryph\p_testproject\a6rkklnznsow", "default", "default", "testproject", "a6rkklnznsow")]
        [InlineData(@"x:\scratch\test\a6rkklnznsow", "default", "scratch", "default", "a6rkklnznsow")]
        [InlineData(@"x:\scratch\test\p_testproject\a6rkklnznsow", "default", "scratch", "testproject", "a6rkklnznsow")]
        [InlineData(@"x:\qa\test\vms\a6rkklnznsow", "qa", "default", "default", "a6rkklnznsow")]
        [InlineData(@"x:\qa\test\vms\p_testproject\a6rkklnznsow", "qa", "default", "testproject", "a6rkklnznsow")]
        [InlineData(@"x:\qa\cluster\test\a6rkklnznsow", "qa", "cluster", "default", "a6rkklnznsow")]
        [InlineData(@"x:\qa\cluster\test\p_testproject\a6rkklnznsow", "qa", "cluster", "testproject", "a6rkklnznsow")]
        [InlineData(@"x:\QA\Cluster\test\p_TestProject\a6rkklnznsow", "qa", "cluster", "testproject", "a6rkklnznsow")]
        public void FromVmPath_ValidPath_ReturnsStorageNames(
            string path,
            string expectedEnvironment,
            string expectedDataStore,
            string expectedProject,
            string expectedIdentifier)
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeSome().Which.Should().Be(expectedEnvironment);
            names.DataStoreName.Should().BeSome().Which.Should().Be(expectedDataStore);
            names.ProjectName.Should().BeSome().Which.Should().Be(expectedProject);
            storageIdentifier.Should().BeSome().Which.Should().Be(expectedIdentifier);
        }

        [Theory]
        [InlineData(@"x:\some\folder")]
        [InlineData(@"x:\default\test\vms\eryph\p_test#42\a6rkklnznsow")]
        // It must not match any default paths for volumes
        [InlineData(@"x:\default\test\volumes\eryph\a6rkklnznsow")]
        [InlineData(@"x:\default\test\volumes\eryph\a6rkklnznsow\test.file")]
        [InlineData(@"x:\default\test\volumes\eryph\p_TestProject\a6rkklnznsow")]
        [InlineData(@"x:\qa\test\volumes\a6rkklnznsow")]
        [InlineData(@"x:\qa\test\volumes\p_TestProject\a6rkklnznsow")]
        public void FromVmPath_InvalidPath_ReturnsNone(string path)
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeNone();
            names.DataStoreName.Should().BeNone();
            names.ProjectName.Should().BeNone();
            storageIdentifier.Should().BeNone();
        }

        [Theory]
        [InlineData(@"x:\default\test\volumes\eryph\a6rkklnznsow", "default", "default", "default",false, "a6rkklnznsow")]
        [InlineData(@"x:\default\test\volumes\eryph\a6rkklnznsow\test.file", "default", "default", "default", false, "a6rkklnznsow")]
        [InlineData(@"x:\default\test\volumes\eryph\p_testproject\a6rkklnznsow", "default", "default", "testproject", false, "a6rkklnznsow")]
        [InlineData(@"x:\scratch\test\a6rkklnznsow", "default", "scratch", "default", false, "a6rkklnznsow")]
        [InlineData(@"x:\scratch\test\p_testproject\a6rkklnznsow", "default", "scratch", "testproject", false, "a6rkklnznsow")]
        [InlineData(@"x:\qa\test\volumes\a6rkklnznsow", "qa", "default", "default",false, "a6rkklnznsow")]
        [InlineData(@"x:\qa\test\volumes\p_testproject\a6rkklnznsow", "qa", "default", "testproject", false, "a6rkklnznsow")]
        [InlineData(@"x:\qa\test\volumes\p_21A549A0-683B-4686-9DD8-40B7D938E495\a6rkklnznsow", "qa", "default", "{21A549A0-683B-4686-9DD8-40B7D938E495}",true, "a6rkklnznsow")]
        [InlineData(@"x:\qa\cluster\test\a6rkklnznsow", "qa", "cluster", "default", false, "a6rkklnznsow")]
        [InlineData(@"x:\qa\cluster\test\p_testproject\a6rkklnznsow", "qa", "cluster", "testproject", false, "a6rkklnznsow")]
        [InlineData(@"x:\QA\Cluster\test\p_TestProject\a6rkklnznsow", "qa", "cluster", "testproject", false, "a6rkklnznsow")]
        public void FromVhdPath_ValidPath_ReturnsStorageNames(
            string path,
            string expectedEnvironment,
            string expectedDataStore,
            string expectedProject,
            bool projectIsGuid,
            string expectedIdentifier)
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeSome().Which.Should().Be(expectedEnvironment);
            names.DataStoreName.Should().BeSome().Which.Should().Be(expectedDataStore);

            if(projectIsGuid)
                names.ProjectId.Should().BeSome().Which.Should().Be(Guid.Parse(expectedProject));
            else
                names.ProjectName.Should().BeSome().Which.Should().Be(expectedProject);

            storageIdentifier.Should().BeSome().Which.Should().Be(expectedIdentifier);
        }

        [Theory]
        [InlineData(@"x:\some\folder")]
        [InlineData(@"x:\default\test\volumes\eryph\p_test#42\a6rkklnznsow")]
        // It must not match any default paths for virtual machines
        [InlineData(@"x:\default\test\vms\eryph\a6rkklnznsow")]
        [InlineData(@"x:\default\test\vms\eryph\a6rkklnznsow\test.file")]
        [InlineData(@"x:\default\test\vms\eryph\p_TestProject\a6rkklnznsow")]
        [InlineData(@"x:\qa\test\vms\a6rkklnznsow")]
        [InlineData(@"x:\qa\test\vms\p_TestProject\a6rkklnznsow")]
        public void FromVhdPath_InvalidPath_ReturnsNone(string path)
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeNone();
            names.DataStoreName.Should().BeNone();
            names.ProjectName.Should().BeNone();
            storageIdentifier.Should().BeNone();
        }

        [Theory]
        [InlineData("default", "default", "default", @"x:\default\test\vms\eryph")]
        [InlineData("default", "cluster", "default", @"x:\cluster\test")]
        [InlineData("qa", "default", "default", @"x:\qa\test\vms")]
        [InlineData("qa", "cluster", "default", @"x:\qa\cluster\test")]
        [InlineData("qa", "scratch", "default", @"x:\scratch\test")]
        [InlineData("default", "default", "test-project", @"x:\default\test\vms\eryph\p_test-project")]
        [InlineData("default", "cluster", "test-project", @"x:\cluster\test\p_test-project")]
        [InlineData("qa", "default", "test-project", @"x:\qa\test\vms\p_test-project")]
        [InlineData("qa", "cluster", "test-project", @"x:\qa\cluster\test\p_test-project")]
        [InlineData("qa", "scratch", "test-project", @"x:\scratch\test\p_test-project")]
        public async Task ResolveVmStorageBasePath_StorageNamesAreValid_ReturnsPath(
            string environmentName,
            string dataStoreName,
            string projectName,
            string expectedPath)
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName,
                ProjectName = projectName,
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeRight().Which.Should().Be(expectedPath);
        }

        [Fact]
        public async Task ResolveVmStorageBasePath_EnvironmentIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "missing-environment",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The environment missing-environment is not configured");
        }

        [Fact]
        public async Task ResolveVmStorageBasePath_DataStoreIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "missing-datastore",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The datastore missing-datastore is not configured");
        }

        [Theory]
        [InlineData("default", "default", "default", @"x:\default\test\volumes\eryph")]
        [InlineData("default", "cluster", "default", @"x:\cluster\test")]
        [InlineData("qa", "default", "default", @"x:\qa\test\volumes")]
        [InlineData("qa", "cluster", "default", @"x:\qa\cluster\test")]
        [InlineData("qa", "scratch", "default", @"x:\scratch\test")]
        [InlineData("default", "default", "test-project", @"x:\default\test\volumes\eryph\p_test-project")]
        [InlineData("default", "cluster", "test-project", @"x:\cluster\test\p_test-project")]
        [InlineData("qa", "default", "test-project", @"x:\qa\test\volumes\p_test-project")]
        [InlineData("qa", "cluster", "test-project", @"x:\qa\cluster\test\p_test-project")]
        [InlineData("qa", "scratch", "test-project", @"x:\scratch\test\p_test-project")]
        public async Task ResolveVolumeStorageBasePath_StorageNamesAreValid_ReturnsPath(
            string environmentName,
            string dataStoreName,
            string projectName,
            string expectedPath)
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = environmentName,
                DataStoreName = dataStoreName,
                ProjectName = projectName,
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeRight().Which.Should().Be(expectedPath);
        }

        [Fact]
        public async Task ResolveVolumeStorageBasePath_EnvironmentIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "missing-environment",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The environment missing-environment is not configured");
        }

        [Fact]
        public async Task ResolveVolumeStorageBasePath_DataStoreIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "missing-datastore",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The datastore missing-datastore is not configured");
        }
    }

    public class DefaultVmHostAgentConfiguration
    {
        private readonly VmHostAgentConfiguration _vmHostAgentConfiguration = new()
        {
            Defaults = new()
            {
                Volumes = @"x:\default\test\volumes\eryph",
                Vms = @"x:\default\test\vms\eryph",
            },
        };

        [Fact]
        public void FromVmPath_ValidPath_ReturnsStorageNames()
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(
                @"x:\default\test\vms\eryph\a6rkklnznsow", _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeSome().Which.Should().Be("default");
            names.DataStoreName.Should().BeSome().Which.Should().Be("default");
            names.ProjectName.Should().BeSome().Which.Should().Be("default");
            storageIdentifier.Should().BeSome().Which.Should().Be("a6rkklnznsow");
        }

        [Fact]
        public void FromVmPath_InvalidPath_ReturnsNone()
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(@"x:\some\folder", _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeNone();
            names.DataStoreName.Should().BeNone();
            names.ProjectName.Should().BeNone();
            storageIdentifier.Should().BeNone();
        }

        [Fact]
        public void FromVhdPath_ValidPath_ReturnsStorageNames()
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(
                @"x:\default\test\volumes\eryph\a6rkklnznsow", _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeSome().Which.Should().Be("default");
            names.DataStoreName.Should().BeSome().Which.Should().Be("default");
            names.ProjectName.Should().BeSome().Which.Should().Be("default");
            storageIdentifier.Should().BeSome().Which.Should().Be("a6rkklnznsow");
        }

        [Fact]
        public void FromVhdPath_InvalidPath_ReturnsNone()
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(@"x:\some\folder", _vmHostAgentConfiguration);

            names.EnvironmentName.Should().BeNone();
            names.DataStoreName.Should().BeNone();
            names.ProjectName.Should().BeNone();
            storageIdentifier.Should().BeNone();
        }

        [Fact]
        public async Task ResolveVmStorageBasePath_StorageNamesAreValid_ReturnsPath()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeRight().Which.Should().Be(@"x:\default\test\vms\eryph");
        }

        [Fact]
        public async Task ResolveVmStorageBasePath_EnvironmentIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "missing-environment",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The environment missing-environment is not configured");
        }

        [Fact]
        public async Task ResolveVmStorageBasePath_DataStoreIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "missing-datastore",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVmStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The datastore missing-datastore is not configured");
        }

        [Fact]
        public async Task ResolveVolumeStorageBasePath_StorageNamesAreValid_ReturnsPath()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeRight().Which.Should().Be(@"x:\default\test\volumes\eryph");
        }

        [Fact]
        public async Task ResolveVolumeStorageBasePath_EnvironmentIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "missing-environment",
                DataStoreName = "default",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The environment missing-environment is not configured");
        }

        [Fact]
        public async Task ResolveVolumeStorageBasePath_DataStoreIsNotConfigured_ReturnsError()
        {
            var storageNames = new StorageNames()
            {
                EnvironmentName = "default",
                DataStoreName = "missing-datastore",
                ProjectName = "default",
            };

            var result = await storageNames.ResolveVolumeStorageBasePath(_vmHostAgentConfiguration);

            result.Should().BeLeft().Which.Message.Should().Be("The datastore missing-datastore is not configured");
        }
    }
}
