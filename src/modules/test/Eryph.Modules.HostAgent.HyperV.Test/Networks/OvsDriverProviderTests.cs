using System.Text;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test.Networks;

using RT = OvsDriverProviderTests.TestRuntime;
using static OvsDriverProvider<OvsDriverProviderTests.TestRuntime>;

public class OvsDriverProviderTests
{
    private readonly RT _runtime;

    private readonly Mock<DirectoryIO> _directoryMock = new();
    private readonly Mock<DismIO> _dismMock = new();
    private readonly Mock<FileIO> _fileMock = new();
    private readonly Mock<IHostNetworkCommands<RT>> _hostNetworkCommandsMock = new();
    private readonly Mock<IPowershellEngine> _powershellEngineMock = new();
    private readonly Mock<ProcessRunnerIO> _processRunnerIOMock = new();
    private readonly Mock<RegistryIO> _registryIOMock = new();

    private const string OvnRunDir = @"Z:\ovnrundir";
    private const string OvnDataDir = @"Z:\ovndatadir";

    private static readonly Guid SwitchId = Guid.NewGuid();

    public OvsDriverProviderTests()
    {
        _runtime = new(new(
            new CancellationTokenSource(),
            _directoryMock.Object,
            _dismMock.Object,
            _fileMock.Object,
            _hostNetworkCommandsMock.Object,
            new NullLoggerFactory(),
            _powershellEngineMock.Object,
            _processRunnerIOMock.Object,
            _registryIOMock.Object));
    }

    [Fact]
    public async Task EnsureDriver_NoDriverInstalledAndInstallAllowed_InstallsDriver()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", false);

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe",
                @"/l ""dbo_ovse.inf"" /c s /i DBO_OVSE",
                @"Z:\ovnrundir\driver",
                false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""))
            .Verifiable();

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        VerifyMocks();
        VerifyOvnDataNotDropped();
    }

    [Fact]
    public async Task EnsureDriver_NoDriverInstalledAndInstallNotAllowed_ReturnFail()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", false);

        var result = await ensureDriver(OvnRunDir, OvnDataDir, false, true)
            .Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Contain("OVS Hyper-V switch extension is missing");

        _processRunnerIOMock.VerifyNoOtherCalls();
        VerifyOvnDataNotDropped();
    }

    [Fact]
    public async Task EnsureDriver_NoDriverInstalledAndTestSignedDriverNotPossible_ReturnFail()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", true);

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Contain("OVS Hyper-V switch extension is missing");

        _processRunnerIOMock.VerifyNoOtherCalls();
        VerifyOvnDataNotDropped();
    }

    [Fact]
    public async Task EnsureDriver_DriverInstalledAndSameVersionInPackage_DriverIsNotModified()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver("1.0.0.0");
        ArrangeDriverPackage("1.0.0.0", false);

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        _processRunnerIOMock.VerifyNoOtherCalls();
        _hostNetworkCommandsMock.Verify(m => m.DisableSwitchExtension(It.IsAny<Guid>()), Times.Never);
        VerifyOvnDataNotDropped();
    }

    [Theory]
    [InlineData("1.0.0.0", "2.0.0.0")]
    [InlineData("2.0.0.0", "1.0.0.0")]
    public async Task EnsureDriver_DriverVersionDiffersAndUpgradeAllowed_DriverIsReplacedAndOvnDataIsDropped(
        string installedVersion, string packageVersion)
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver(installedVersion);
        ArrangeDriverPackage(packageVersion, false);
        ArrangeOvnDataDirExists(true);

        Guid otherSwitchId = Guid.NewGuid();

        _hostNetworkCommandsMock.Setup(m => m.GetSwitchExtensions())
            .Returns(SuccessAff<RT, Seq<VMSwitchExtension>>(Seq(
                new VMSwitchExtension()
                {
                    Enabled = true,
                    SwitchId = SwitchId,
                    SwitchName = EryphConstants.OverlaySwitchName,
                },
                new VMSwitchExtension() { Enabled = false, SwitchId = otherSwitchId })))
            .Verifiable();

        _hostNetworkCommandsMock.Setup(m => m.DisableSwitchExtension(SwitchId))
            .Returns(SuccessAff<RT, Unit>(unit))
            .Verifiable();

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe", "/u DBO_OVSE", "", false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""))
            .Verifiable();

        _processRunnerIOMock.SetupSequence(m => m.RunProcess(
                "sc.exe", "query type=driver", "", false))
            .ReturnsAsync(new ProcessRunnerResult(
                exitCode: 0, output: "lorem\nSERVICE_NAME: DBO_OVSE\nipsum"))
            .ReturnsAsync(new ProcessRunnerResult(exitCode: 0, output: "lorem\nipsum"));

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "pnputil.exe", "/delete-driver oem100.inf /force", "", false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""))
            .Verifiable();

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "pnputil.exe", "/delete-driver oem101.inf /force", "", false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""))
            .Verifiable();

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe",
                @"/l ""dbo_ovse.inf"" /c s /i DBO_OVSE",
                @"Z:\ovnrundir\driver",
                false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""))
            .Verifiable();

        _hostNetworkCommandsMock.Setup(m => m.EnableSwitchExtension(SwitchId))
            .Returns(SuccessAff<RT, Unit>(unit))
            .Verifiable();

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        VerifyMocks();
        _hostNetworkCommandsMock.Verify(m => m.DisableSwitchExtension(otherSwitchId), Times.Never);
        _directoryMock.Verify(m => m.Delete(OvnDataDir, true), Times.Once);
    }

    [Fact]
    public async Task EnsureDriver_DriverUpgradeNecessaryAndOvnDataMissing_NoDeleteAttempted()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver("1.0.0.0");
        ArrangeDriverPackage("2.0.0.0", false);
        ArrangeOvnDataDirExists(false);

        _hostNetworkCommandsMock.Setup(m => m.GetSwitchExtensions())
            .Returns(SuccessAff<RT, Seq<VMSwitchExtension>>(Seq<VMSwitchExtension>()));

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe", "/u DBO_OVSE", "", false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""));

        _processRunnerIOMock.SetupSequence(m => m.RunProcess(
                "sc.exe", "query type=driver", "", false))
            .ReturnsAsync(new ProcessRunnerResult(exitCode: 0, output: "lorem\nipsum"));

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "pnputil.exe", It.IsAny<string>(), "", false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""));

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe",
                @"/l ""dbo_ovse.inf"" /c s /i DBO_OVSE",
                @"Z:\ovnrundir\driver",
                false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""));

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        _directoryMock.Verify(m => m.Exists(OvnDataDir), Times.Once);
        _directoryMock.Verify(m => m.Delete(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task EnsureDriver_DriverUpgradeNecessaryAndUpgradeNotAllowed_DriverIsNotModified()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver("1.0.0.0");
        ArrangeDriverPackage("2.0.0.0", false);

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, false)
            .Run(_runtime);

        result.Should().BeSuccess();

        _processRunnerIOMock.VerifyNoOtherCalls();
        _hostNetworkCommandsMock.Verify(m => m.DisableSwitchExtension(It.IsAny<Guid>()), Times.Never);
        VerifyOvnDataNotDropped();
    }

    [Fact]
    public async Task EnsureDriver_DriverUpgradeNecessaryAndTestSignedDriverNotPossible_DriverIsNotModified()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver("1.0.0.0");
        ArrangeDriverPackage("2.0.0.0", true);

        var result = await ensureDriver(OvnRunDir, OvnDataDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        _processRunnerIOMock.VerifyNoOtherCalls();
        _hostNetworkCommandsMock.Verify(m => m.DisableSwitchExtension(It.IsAny<Guid>()), Times.Never);
        VerifyOvnDataNotDropped();
    }


    private void ArrangeIsTestSigningEnabled(bool isEnabled)
    {
        _registryIOMock.Setup(m => m.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                "SystemStartOptions"))
            .Returns(isEnabled ? "LOREM TESTSIGNING IPSUM" : "LOREM IPSUM");
    }

    private void ArrangeOvnDataDirExists(bool exists)
    {
        _directoryMock.Setup(m => m.Exists(OvnDataDir)).Returns(exists);
        if (exists)
        {
            _directoryMock.Setup(m => m.Delete(OvnDataDir, true)).Verifiable();
        }
    }

    private void VerifyOvnDataNotDropped()
    {
        _directoryMock.Verify(m => m.Delete(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    private void ArrangeInstalledDriver(string version)
    {
        _hostNetworkCommandsMock.Setup(m => m.GetInstalledSwitchExtension())
            .Returns(SuccessAff(Optional(new VMSystemSwitchExtension(){ Version = version})));

        _dismMock.Setup(m => m.GetInstalledDriverPackages())
            .ReturnsAsync(Seq(
                new DismDriverInfo()
                {
                    OriginalFileName = @"Z:\driver\100\dbo_ovse.inf",
                    Driver = "oem100.inf",
                },
                new DismDriverInfo()
                {
                    OriginalFileName = @"Z:\driver\101\dbo_ovse.inf",
                    Driver = "oem101.inf",
                },
                new DismDriverInfo()
                {
                    OriginalFileName = @"Z:\driver\102\acme_corp.inf",
                    Driver = "oem102.inf",
                }));
    }

    private void ArrangeNoDriverInstalled()
    {
        _hostNetworkCommandsMock.Setup(m => m.GetInstalledSwitchExtension())
            .Returns(SuccessAff(Option<VMSystemSwitchExtension>.None));
    }

    private void ArrangeDriverPackage(string version, bool isTestSigned)
    {
        _fileMock.Setup(m => m.ReadAllBytes(Path.Combine(@"Z:\ovnrundir", "driver", "dbo_ovse.inf"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.ASCII.GetBytes($"""
                DriverVer = 4/2/2020,{version}
                """));

        var isGetSignatureCommand = (PsCommandBuilder builder) =>
        {
            var chain = builder.ToChain();
            return chain.Length >= 2
               && chain[0] is PsCommandBuilder.CommandPart { Command: "Get-AuthenticodeSignature" }
               && chain[1] is PsCommandBuilder.ParameterPart
               {
                   Parameter: "FilePath",
                   Value: @"Z:\ovnrundir\driver\dbo_ovse.cat",
               };
        };

        _powershellEngineMock.Setup(m => m.GetObjectValuesAsync<string?>(
                It.Is<PsCommandBuilder>(b => isGetSignatureCommand(b)), null, false, CancellationToken.None))
            .Returns(RightAsync<Error, Seq<string?>>(Seq1<string?>(
                isTestSigned ? "Some Test Publisher" : "Microsoft Windows Hardware Compatibility Publisher")));
    }

    private void VerifyMocks()
    {
        _fileMock.VerifyAll();
        _hostNetworkCommandsMock.VerifyAll();
        _powershellEngineMock.VerifyAll();
        _processRunnerIOMock.VerifyAll();
        _registryIOMock.VerifyAll();
    }

    public readonly struct TestRuntime(TestRuntimeEnv env) :
        HasDirectory<RT>,
        HasDism<RT>,
        HasFile<RT>,
        HasHostNetworkCommands<RT>,
        HasLogger<RT>,
        HasProcessRunner<RT>,
        HasPowershell<RT>,
        HasRegistry<RT>
    {
        public readonly TestRuntimeEnv Env = env;

        public RT LocalCancel => new(new TestRuntimeEnv(
            new CancellationTokenSource(),
            Env.Directory,
            Env.Dism,
            Env.File,
            Env.HostNetworkCommands,
            Env.LoggerFactory,
            Env.PowershellEngine,
            Env.ProcessRunner,
            Env.Registry));

        public CancellationToken CancellationToken => Env.CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => Env.CancellationTokenSource;

        public Eff<RT, DirectoryIO> DirectoryEff => Eff<RT, DirectoryIO>(rt => rt.Env.Directory);

        public Eff<RT, DismIO> DismEff => Eff<RT, DismIO>(rt => rt.Env.Dism);

        public Encoding Encoding => Encoding.UTF8;

        public Eff<RT, FileIO> FileEff => Eff<RT, FileIO>(rt => rt.Env.File);

        public Eff<RT, IHostNetworkCommands<RT>> HostNetworkCommands =>
            Eff<RT, IHostNetworkCommands<RT>>(rt => rt.Env.HostNetworkCommands);

        public Eff<RT, ILogger> Logger(string category) => Eff<RT, ILogger>(rt => rt.Env.LoggerFactory.CreateLogger(category));

        public Eff<RT, ILogger<T>> Logger<T>() => Eff<RT, ILogger<T>>(rt => rt.Env.LoggerFactory.CreateLogger<T>());

        public Eff<RT, IPowershellEngine> Powershell => Eff<RT, IPowershellEngine>(rt => rt.Env.PowershellEngine);

        public Eff<RT, ProcessRunnerIO> ProcessRunnerEff => Eff<RT, ProcessRunnerIO>(rt => rt.Env.ProcessRunner);

        public Eff<RT, RegistryIO> RegistryEff => Eff<RT, RegistryIO>(rt => rt.Env.Registry);
    }

    public class TestRuntimeEnv(
        CancellationTokenSource cancellationTokenSource,
        DirectoryIO directory,
        DismIO dism,
        FileIO file,
        IHostNetworkCommands<RT> hostNetworkCommands,
        ILoggerFactory loggerFactory,
        IPowershellEngine powershellEngine,
        ProcessRunnerIO processRunner,
        RegistryIO registry)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public DirectoryIO Directory { get; } = directory;

        public DismIO Dism { get; } = dism;

        public FileIO File { get; } = file;

        public IHostNetworkCommands<RT> HostNetworkCommands { get; } = hostNetworkCommands;

        public ILoggerFactory LoggerFactory { get; } = loggerFactory;

        public IPowershellEngine PowershellEngine { get; } = powershellEngine;

        public ProcessRunnerIO ProcessRunner { get; } = processRunner;

        public RegistryIO Registry { get; } = registry;
    }
}
