using Eryph.VmManagement;

namespace Eryph.Modules.GenePoolModule.Test;

public class PathUtilsTests
{
    [Theory]
    [InlineData(@"Z:\eryph", @"Z:\eryph\a\b\c", @"a\b\c")]
    [InlineData(@"Z:\eryph", @"Z:\eryph\data.json", "data.json")]
    public void GetContainedPath_WhenPathIsContained_ReturnsNone(
        string relativeTo, string path, string expected)
    {
        var result = PathUtils.GetContainedPath(relativeTo, path);
        result.Should().BeSome().Which.Should().Be(expected);
    }

    [Theory]
    [InlineData(@"Z:\eryph\", @"Y:\test")]
    [InlineData(@"Z:\eryph\", @"Z:\test")]
    [InlineData(@"Z:\eryph\", @"Z:\eryph")]
    public void GetContainedPath_WhenPathIsNotContained_ReturnsNone(
        string relativeTo, string path)
    {
        var result = PathUtils.GetContainedPath(relativeTo, path);
        result.Should().BeNone();
    }
}
