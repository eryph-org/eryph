using Eryph.Core.VmAgent;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Xunit;

namespace Eryph.Modules.VmHostAgent.Test
{
    public class StorageNamesTests
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
                    Name = "test-cluster",
                    Path = @"x:\cluster\test"
                },
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "test-scratch",
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
                            Name = "prod-cluster",
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
                            Name = "qa-cluster",
                            Path = @"x:\qa\cluster\test"
                        },
                    }
                }
            },
        };

        [Theory]
        [InlineData(@"x:\default\test\vms\eryph\A6RKKLNZNSOW", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\vms\eryph\A6RKKLNZNSOW\test.file", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\vms\eryph\p_TestProject\A6RKKLNZNSOW", "default", "default", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\scratch\test\A6RKKLNZNSOW", "default", "test-scratch", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\scratch\test\p_TestProject\A6RKKLNZNSOW", "default", "test-scratch", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\vms\A6RKKLNZNSOW", "qa", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\vms\p_TestProject\A6RKKLNZNSOW", "qa", "default", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\cluster\test\A6RKKLNZNSOW", "qa", "qa-cluster", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\cluster\test\p_TestProject\A6RKKLNZNSOW", "qa", "qa-cluster", "testproject", "A6RKKLNZNSOW")]
        public void FromVmPath_ValidPath_ReturnsStorageNames(
            string path,
            string expectedEnvironment,
            string expectedDataStore,
            string expectedProject,
            string expectedIdentifier)
        {
            var (names, storageIdentifier) = StorageNames.FromVmPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.IsSome.Should().Be(true);
            names.DataStoreName.IsSome.Should().Be(true);
            names.ProjectName.IsSome.Should().Be(true);
            storageIdentifier.IsSome.Should().Be(true);

            
            names.EnvironmentName.ValueUnsafe().Should().Be(expectedEnvironment);
            names.DataStoreName.ValueUnsafe().Should().Be(expectedDataStore);
            names.ProjectName.ValueUnsafe().Should().Be(expectedProject);

            storageIdentifier.ValueUnsafe().Should().Be(expectedIdentifier);
        }

        [Theory]
        //[InlineData("not-a-path")]
        [InlineData(@"x:\some\folder")]
        // It must not match any default paths for volumes
        [InlineData(@"x:\default\test\volumes\eryph\A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\volumes\eryph\A6RKKLNZNSOW\test.file")]
        [InlineData(@"x:\default\test\volumes\eryph\p_TestProject\A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\volumes\A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\volumes\p_TestProject\A6RKKLNZNSOW")]
        public void FromVmPath_InvalidPath_ReturnsNone(string path)
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.IsNone.Should().Be(true);
            names.DataStoreName.IsNone.Should().Be(true);
            names.ProjectName.IsNone.Should().Be(true);
            storageIdentifier.IsNone.Should().Be(true);
        }

        [Theory]
        [InlineData(@"x:\default\test\volumes\eryph\A6RKKLNZNSOW", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\volumes\eryph\A6RKKLNZNSOW\test.file", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\volumes\eryph\p_TestProject\A6RKKLNZNSOW", "default", "default", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\scratch\test\A6RKKLNZNSOW", "default", "test-scratch", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\scratch\test\p_TestProject\A6RKKLNZNSOW", "default", "test-scratch", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\volumes\A6RKKLNZNSOW", "qa", "default", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\volumes\p_TestProject\A6RKKLNZNSOW", "qa", "default", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\cluster\test\A6RKKLNZNSOW", "qa", "qa-cluster", "default", "A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\cluster\test\p_TestProject\A6RKKLNZNSOW", "qa", "qa-cluster", "testproject", "A6RKKLNZNSOW")]
        [InlineData(@"x:\\default\test\volumes\eryph\genepool\testorg\testgene\testversion\volumes\test.vhdx", "default", "default", "default", "gene:testorg/testgene/testversion:test")]
        public void FromVhdPath_ValidPath_ReturnsStorageNames(
            string path,
            string expectedEnvironment,
            string expectedDataStore,
            string expectedProject,
            string expectedIdentifier)
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(path, _vmHostAgentConfiguration);

            names.ProjectName.IsSome.Should().Be(true);
            names.EnvironmentName.IsSome.Should().Be(true);
            names.DataStoreName.IsSome.Should().Be(true);
            storageIdentifier.IsSome.Should().Be(true);

            names.EnvironmentName.ValueUnsafe().Should().Be(expectedEnvironment);
            names.DataStoreName.ValueUnsafe().Should().Be(expectedDataStore);
            names.ProjectName.ValueUnsafe().Should().Be(expectedProject);

            storageIdentifier.ValueUnsafe().Should().Be(expectedIdentifier);
        }

        [Theory]
        //[InlineData("not-a-path")]
        [InlineData(@"x:\some\folder")]
        // It must not match any default paths for virtual machines
        [InlineData(@"x:\default\test\vms\eryph\A6RKKLNZNSOW")]
        [InlineData(@"x:\default\test\vms\eryph\A6RKKLNZNSOW\test.file")]
        [InlineData(@"x:\default\test\vms\eryph\p_TestProject\A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\vms\A6RKKLNZNSOW")]
        [InlineData(@"x:\qa\test\vms\p_TestProject\A6RKKLNZNSOW")]
        public void FromVhdPath_InvalidPath_ReturnsNone(string path)
        {
            var (names, storageIdentifier) = StorageNames.FromVhdPath(path, _vmHostAgentConfiguration);

            names.EnvironmentName.IsNone.Should().Be(true);
            names.DataStoreName.IsNone.Should().Be(true);
            names.ProjectName.IsNone.Should().Be(true);
            storageIdentifier.IsNone.Should().Be(true);
        }

        [Theory]
        [InlineData("default", "default", "default", @"x:\default\test\vms\eryph")]
        [InlineData("default", "test-cluster", "default", @"x:\cluster\test")]
        [InlineData("qa", "default", "default", @"x:\qa\test\vms")]
        [InlineData("qa", "qa-cluster", "default", @"x:\qa\cluster\test")]
        [InlineData("qa", "test-scratch", "default", @"x:\scratch\test")]
        [InlineData("default", "default", "test-project", @"x:\default\test\vms\eryph\p_test-project")]
        [InlineData("default", "test-cluster", "test-project", @"x:\cluster\test\p_test-project")]
        [InlineData("qa", "default", "test-project", @"x:\qa\test\vms\p_test-project")]
        [InlineData("qa", "qa-cluster", "test-project", @"x:\qa\cluster\test\p_test-project")]
        [InlineData("qa", "test-scratch", "test-project", @"x:\scratch\test\p_test-project")]
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

            result.IsRight.Should().BeTrue();
            result.ValueUnsafe().Should().Be(expectedPath);
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

            result.IsRight.Should().BeFalse();
            result.IfLeft(e => e.Message.Should().Be("The environment missing-environment is not configured"));
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

            result.IsRight.Should().BeFalse();
            result.IfLeft(e => e.Message.Should().Be("The datastore missing-datastore is not configured"));
        }

        [Theory]
        [InlineData("default", "default", "default", @"x:\default\test\volumes\eryph")]
        [InlineData("default", "test-cluster", "default", @"x:\cluster\test")]
        [InlineData("qa", "default", "default", @"x:\qa\test\volumes")]
        [InlineData("qa", "qa-cluster", "default", @"x:\qa\cluster\test")]
        [InlineData("qa", "test-scratch", "default", @"x:\scratch\test")]
        [InlineData("default", "default", "test-project", @"x:\default\test\volumes\eryph\p_test-project")]
        [InlineData("default", "test-cluster", "test-project", @"x:\cluster\test\p_test-project")]
        [InlineData("qa", "default", "test-project", @"x:\qa\test\volumes\p_test-project")]
        [InlineData("qa", "qa-cluster", "test-project", @"x:\qa\cluster\test\p_test-project")]
        [InlineData("qa", "test-scratch", "test-project", @"x:\scratch\test\p_test-project")]
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
            result.IsRight.Should().BeTrue();
            result.ValueUnsafe().Should().Be(expectedPath);
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

            result.IsRight.Should().BeFalse();
            result.IfLeft(e => e.Message.Should().Be("The environment missing-environment is not configured"));
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

            result.IsRight.Should().BeFalse();
            result.IfLeft(e => e.Message.Should().Be("The datastore missing-datastore is not configured"));
        }
    }
}
