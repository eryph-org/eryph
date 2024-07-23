using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement.Sys;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Effects.Traits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test;

using RT = SystemRequirementsCheckerTests.TestRuntime;
using static SystemRequirementsChecker<SystemRequirementsCheckerTests.TestRuntime>;

public class SystemRequirementsCheckerTests
{
    private readonly Mock<WmiIO> _wmiMock = new();

    private readonly RT _runtime;

    public SystemRequirementsCheckerTests()
    {
        _runtime = new RT(new TestRuntimeEnv(
            new CancellationTokenSource(),
            new NullLoggerFactory(),
            _wmiMock.Object));
    }

    [Theory, CombinatorialData]
    public async Task EnsureHyperV_HyperVIsAvailable_ReturnsSuccess(
        bool isService)
    {
        _wmiMock.Setup(wmi => wmi.ExecuteQuery(
                @"\Root\CIMv2",
                "SELECT Name, InstallState FROM Win32_OptionalFeature"))
            .Returns(Seq(
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V")),
                    ("InstallState", Optional<object>(1u))),
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V-Management-PowerShell")),
                    ("InstallState", Optional<object>(1u)))
            ));

        _wmiMock.Setup(wmi => wmi.ExecuteQuery(
                @"\Root\Virtualization\v2",
                "SELECT DefaultExternalDataRoot, DefaultVirtualHardDiskPath FROM Msvm_VirtualSystemManagementServiceSettingData"))
            .Returns(Seq1(HashMap(
                    ("DefaultExternalDataRoot", Optional<object>(@"X:\disks")),
                    ("DefaultVirtualHardDiskPath", Optional<object>(@"X:\vms"))
            )));

        var result = await ensureHyperV(isService).Run(_runtime);

        result.Should().BeSuccess();
    }

    [Theory, CombinatorialData]
    public async Task EnsureHyper_FeatureNotInstalled_ReturnsError(
        bool isService)
    {
        _wmiMock.Setup(wmi => wmi.ExecuteQuery(
                @"\Root\CIMv2",
                "SELECT Name, InstallState FROM Win32_OptionalFeature"))
            .Returns(Seq(
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V")),
                    ("InstallState", Optional<object>(3u))),
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V-Management-PowerShell")),
                    ("InstallState", Optional<object>(3u)))
            ));

        var result = await ensureHyperV(isService).Run(_runtime);

        result.Should().BeFail().Which.Message
            .Should().Be("Hyper-V platform (Microsoft-Hyper-V) is not installed.");
    }

    [Fact]
    public async Task EnsureHyperV_QueriesFailWhenRunningAsService_FailedQueriesAreRetried()
    {
        _wmiMock.SetupSequence(wmi => wmi.ExecuteQuery(
                @"\Root\CIMv2",
                "SELECT Name, InstallState FROM Win32_OptionalFeature"))
            .Throws(new ManagementException("error"))
            .Returns(Seq(
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V")),
                    ("InstallState", Optional<object>(1u))),
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V-Management-PowerShell")),
                    ("InstallState", Optional<object>(1u)))
            ));

        _wmiMock.SetupSequence(wmi => wmi.ExecuteQuery(
                @"\Root\Virtualization\v2",
                "SELECT DefaultExternalDataRoot, DefaultVirtualHardDiskPath FROM Msvm_VirtualSystemManagementServiceSettingData"))
            .Throws(new ManagementException("error"))
            .Returns(Seq1(HashMap(
                ("DefaultExternalDataRoot", Optional<object>(@"X:\disks")),
                ("DefaultVirtualHardDiskPath", Optional<object>(@"X:\vms"))
            )));

        var result = await ensureHyperV(true).Run(_runtime);

        result.Should().BeSuccess();
    }

    [Fact]
    public async Task EnsureHyperV_QueriesReturnsNothingWhenRunningAsService_QueriesAreRetried()
    {
        _wmiMock.SetupSequence(wmi => wmi.ExecuteQuery(
                @"\Root\CIMv2",
                "SELECT Name, InstallState FROM Win32_OptionalFeature"))
            .Returns(Seq<HashMap<string, Option<object>>>())
            .Returns(Seq(
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V")),
                    ("InstallState", Optional<object>(1u))),
                HashMap(("Name", Optional<object>("Microsoft-Hyper-V-Management-PowerShell")),
                    ("InstallState", Optional<object>(1u)))
            ));

        _wmiMock.SetupSequence(wmi => wmi.ExecuteQuery(
                @"\Root\Virtualization\v2",
                "SELECT DefaultExternalDataRoot, DefaultVirtualHardDiskPath FROM Msvm_VirtualSystemManagementServiceSettingData"))
            .Returns(Seq<HashMap<string, Option<object>>>())
            .Returns(Seq1(HashMap(
                ("DefaultExternalDataRoot", Optional<object>(@"X:\disks")),
                ("DefaultVirtualHardDiskPath", Optional<object>(@"X:\vms"))
            )));

        var result = await ensureHyperV(true).Run(_runtime);

        result.Should().BeSuccess();
    }

    public readonly struct TestRuntime(TestRuntimeEnv env) : HasCancel<RT>, HasLogger<RT>, HasWmi<RT>
    {
        private readonly TestRuntimeEnv _env = env;

        public RT LocalCancel => new(new TestRuntimeEnv(new CancellationTokenSource(), _env.LoggerFactory, _env.Wmi));

        public CancellationToken CancellationToken => _env.CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => _env.CancellationTokenSource;

        public Eff<RT, WmiIO> WmiEff => Eff<RT, WmiIO>(rt => rt._env.Wmi);

        public Eff<RT, ILogger> Logger(string category) =>
            Eff<RT, ILogger>(rt => rt._env.LoggerFactory.CreateLogger(category));

        public Eff<RT, ILogger<T>> Logger<T>() =>
            Eff<RT, ILogger<T>>(rt => rt._env.LoggerFactory.CreateLogger<T>());
    }

    public class TestRuntimeEnv(
        CancellationTokenSource cancellationTokenSource,
        ILoggerFactory loggerFactory,
        WmiIO wmi)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public ILoggerFactory LoggerFactory { get; } = loggerFactory;

        public WmiIO Wmi { get; } = wmi;
    }
}
