using System.Runtime.Versioning;
using Eryph.Core.Sys;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Sys;

public sealed class FileSystemTests : IDisposable
{
    private readonly string _testDirectoryPath;

    public FileSystemTests()
    {
        _testDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectoryPath);
    }

    [Fact]
    public async Task IsInUse_FileIsLocked_ReturnsTrue()
    {
        var filePath = Path.Combine(_testDirectoryPath, Path.GetRandomFileName());
        await using var _ = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var result = await Run(FileSystem<TestRuntime>.isInUse(filePath));
        result.Should().BeSuccess().Which.Should().BeTrue();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    public async Task IsInUse_FilePartIsLocked_ReturnsFalse()
    {
        var filePath = Path.Combine(_testDirectoryPath, Path.GetRandomFileName());
        await using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        fileStream.Lock(42, 1);
        var result = await Run(FileSystem<TestRuntime>.isInUse(filePath));
        result.Should().BeSuccess().Which.Should().BeTrue();
    }

    [Fact]
    public async Task IsInUse_FileDoesNotExist_ReturnsFalse()
    {
        var filePath = Path.Combine(_testDirectoryPath, Path.GetRandomFileName());
        var result = await Run(FileSystem<TestRuntime>.isInUse(filePath));
        result.Should().BeSuccess().Which.Should().BeFalse();
    }

    [Fact]
    public async Task IsInUse_FileIsNotLocked_ReturnsFalse()
    {
        var filePath = Path.Combine(_testDirectoryPath, Path.GetRandomFileName());
        await using (var _ = File.Create(filePath))
        {
            // Immediately close the file to ensure it's not locked
        }
        var result = await Run(FileSystem<TestRuntime>.isInUse(filePath));
        result.Should().BeSuccess().Which.Should().BeFalse();
    }

    private async ValueTask<Fin<T>> Run<T>(Aff<TestRuntime, T> logic)
    {
        using var cts = new CancellationTokenSource();
        return await logic.Run(new TestRuntime(cts));
    }

    public void Dispose()
    {
        Directory.Delete(_testDirectoryPath, true);
    }

    private readonly struct TestRuntime : HasFileSystem<TestRuntime>
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TestRuntime(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public TestRuntime LocalCancel => new(new CancellationTokenSource());

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => _cancellationTokenSource;

        public Eff<TestRuntime, FileSystemIO> FileSystemEff => SuccessEff(LiveFileSystemIO.Default);
    }
}
