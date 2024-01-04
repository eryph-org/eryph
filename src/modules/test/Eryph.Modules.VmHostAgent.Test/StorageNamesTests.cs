using Eryph.Core.VmAgent;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using LanguageExt.UnsafeValueAccess;
using Xunit;

namespace Eryph.Modules.VmHostAgent.Test
{
    public class StorageNamesTests
    {

        [Theory]
        [InlineData("c:\\default\\test\\eryph\\A6RKKLNZNSOW", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData("c:\\default\\test\\eryph\\A6RKKLNZNSOW\\test.file", "default", "default", "default", "A6RKKLNZNSOW")]
        [InlineData("c:\\default\\test\\eryph\\p_Test3\\A6RKKLNZNSOW", "test3", "default", "default", "A6RKKLNZNSOW")]
        [InlineData("c:\\default\\test\\eryph\\genepool\\testorg\\testgene\\version\\volumes\\test.vhdx", "default", "default", "default", "gene:testorg/testgene/version:test")]
        public void Resolves_Path_To_Expected_Storage_names(string path, string project, string environment,  string dataStore, string identifier)
        {
            var (names, storageIdentifier) = StorageNames.FromPath(path, new VmHostAgentConfiguration(), "c:\\default\\test\\eryph");

            names.ProjectName.IsSome.Should().Be(true);
            names.EnvironmentName.IsSome.Should().Be(true);
            names.DataStoreName.IsSome.Should().Be(true);
            storageIdentifier.IsSome.Should().Be(true);

            names.ProjectName.ValueUnsafe().Should().Be(project);
            names.EnvironmentName.ValueUnsafe().Should().Be(environment);
            names.DataStoreName.ValueUnsafe().Should().Be(dataStore);

            storageIdentifier.ValueUnsafe().Should().Be(identifier);

        }
    }
}
