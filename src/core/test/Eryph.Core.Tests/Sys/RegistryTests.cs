using Eryph.Core.Sys;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Sys;

/// <summary>
/// These are integration tests for the <see cref="Registry{RT}"/> which
/// access the real Windows registry. These tests will only work on Windows.
/// </summary>
public class RegistryTests
{
    [Fact]
    public void GetRegistryValue_ValueExists_ReturnsValue()
    {
        var result = Run(Registry<TestRuntime>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            "SystemRoot"));

        result.Should().BeSuccess()
            .Which.Should().Be(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    }

    [Fact]
    public void GetRegistryValue_KeyDoesNotExist_ReturnsNone()
    {
        var result = Run(Registry<TestRuntime>.getRegistryValue(
            $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\{Guid.NewGuid()}",
            "SystemRoot"));

        result.Should().BeSuccess().Which.Should().BeNone();
    }

    [Fact]
    public void GetRegistryValue_ValueDoesNotExist_ReturnsNone()
    {
        var result = Run(Registry<TestRuntime>.getRegistryValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            Guid.NewGuid().ToString()));

        result.Should().BeSuccess().Which.Should().BeNone();
    }

    [Fact]
    public void GetRegistryValue_InvalidHive_ReturnsFail()
    {
        var result = Run(Registry<TestRuntime>.getRegistryValue(
            $"{Guid.NewGuid()}",
            Guid.NewGuid().ToString()));

        result.Should().BeFail()
            .Which.Exception.Should().BeSome()
            .Which.Should().BeOfType<ArgumentException>();
    }

    private Fin<Option<object>> Run(Eff<TestRuntime, Option<object>> logic)
    {
        return logic.Run(new TestRuntime());
    }

    private readonly struct TestRuntime : HasRegistry<TestRuntime>
    {
        public Eff<TestRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);
    }
}
