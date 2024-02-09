using Eryph.Core.Sys;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

using RT = OvsDriverProviderTests.TestRuntime;

public class OvsDriverProviderTests
{
    

    private const string OvsRunDir = @"Z:\ovsrundir";
    private readonly RT _runtime;

    private readonly Mock<EnvironmentIO> _environmentMock = new();
    private readonly Mock<FileIO> _fileMock = new();
    private readonly Mock<IHostNetworkCommands<RT>> _hostNetworkCommandsMock = new();
    private readonly Mock<IPowershellEngine> _powershellEngineMock = new();
    private readonly Mock<ProcessRunnerIO> _processRunnerIOMock = new();
    private readonly Mock<RegistryIO> _registryIOMock = new();

    public OvsDriverProviderTests()
    {
        _runtime = new TestRuntime(new TestRuntimeEnv(
            new CancellationTokenSource(),
            _environmentMock.Object,
            _fileMock.Object,
            _hostNetworkCommandsMock.Object,
            new NullLoggerFactory(),
            _powershellEngineMock.Object,
            _processRunnerIOMock.Object,
            _registryIOMock.Object));
    }

    [Fact]
    public async Task EnsureDriver_NoDriverAndInstallAllowed_InstallsDriver()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", false);

        _processRunnerIOMock.Setup(m => m.RunProcess(
                "netcfg.exe",
                @"-l ""dbo_ovse.inf"" -c s -i DBO_OVSE",
                @"Z:\ovsrundir\driver",
                false))
            .ReturnsAsync(new ProcessRunnerResult(0, ""));

        _hostNetworkCommandsMock.Setup(m => m.EnableSwitchExtension())
            .Returns(SuccessAff<RT, Unit>(unit))
            .Verifiable();

        var result = await OvsDriverProvider<RT>.ensureDriver(OvsRunDir, true, true)
            .Run(_runtime);

        result.Should().BeSuccess();

        VerifyMocks();
    }

    [Fact]
    public async Task EnsureDriver_NoDriverInstalledAndInstallNotAllowed_ReturnFail()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", false);

        var result = await OvsDriverProvider<RT>.ensureDriver(OvsRunDir, false, true)
            .Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Contain("OVS Hyper-V switch extension is missing");

        _processRunnerIOMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureDriver_NoDriverInstalledAndTestSignedDriverNotPossible_ReturnFail()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeNoDriverInstalled();
        ArrangeDriverPackage("1.0.0.0", true);

        var result = await OvsDriverProvider<RT>.ensureDriver(OvsRunDir, true, true)
            .Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Contain("OVS Hyper-V switch extension is missing");

        _processRunnerIOMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnsureDriver_DriverInstalledAndSomeVersionInPackage_DriverIsNotModified()
    {
        ArrangeIsTestSigningEnabled(false);
        ArrangeInstalledDriver("1.0.0.0");
        ArrangeDriverPackage("1.0.0.0", false);

        var result = await OvsDriverProvider<RT>.ensureDriver(OvsRunDir, true, true)
            .Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Contain("OVS Hyper-V switch extension is missing");

        _processRunnerIOMock.VerifyNoOtherCalls();
    }


    private void ArrangeIsTestSigningEnabled(bool isEnabled)
    {
        _registryIOMock.Setup(m => m.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                "SystemStartOptions"))
            .Returns(isEnabled ? "LOREM TESTSIGNING IPSUM" : "LOREM IPSUM");
    }

    private void ArrangeInstalledDriver(string version)
    {
        _hostNetworkCommandsMock.Setup(m => m.GetInstalledSwitchExtension())
            .Returns(SuccessAff(Optional(new VMSystemSwitchExtension(){ Version = version})));
    }

    private void ArrangeNoDriverInstalled()
    {
        _hostNetworkCommandsMock.Setup(m => m.GetInstalledSwitchExtension())
            .Returns(SuccessAff(Option<VMSystemSwitchExtension>.None));
    }

    private void ArrangeDriverPackage(string version, bool isTestSigned)
    {
        _fileMock.Setup(m => m.ReadAllBytes(Path.Combine(OvsRunDir, "driver", "dbo_ovse.inf"),
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
                   Value: @"Z:\ovsrundir\driver\dbo_ovse.cat",
               };
        };

        _powershellEngineMock.Setup(m => m.GetObjectValuesAsync<string?>(
                It.Is<PsCommandBuilder>(b => isGetSignatureCommand(b)), null))
            .Returns(EitherAsync<PowershellFailure, Seq<string?>>.Right(
                SeqOne(isTestSigned
                    ? "Some Test Publisher"
                    : "Microsoft Windows Hardware Compatibility Publisher")));
    }

    private void ArrangeNoDriverPackage(string version)
    {

    }

    private void VerifyMocks()
    {
        _environmentMock.VerifyAll();
        _fileMock.VerifyAll();
        _hostNetworkCommandsMock.VerifyAll();
        _powershellEngineMock.VerifyAll();
        _processRunnerIOMock.VerifyAll();
        _registryIOMock.VerifyAll();
    }

    public readonly struct TestRuntime(TestRuntimeEnv env) :
        HasEnvironment<RT>,
        HasFile<RT>,
        HasHostNetworkCommands<RT>,
        HasLogger<RT>,
        HasProcessRunner<RT>,
        HasPowershell<RT>,
        HasRegistry<RT>
    {
        public readonly TestRuntimeEnv Env = env;

        public TestRuntime LocalCancel => new(new TestRuntimeEnv(
            new CancellationTokenSource(),
            Env.Environment,
            Env.File,
            Env.HostNetworkCommands,
            Env.LoggerFactory,
            Env.PowershellEngine,
            Env.ProcessRunner,
            Env.Registry));

        public CancellationToken CancellationToken => Env.CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => Env.CancellationTokenSource;

        public Encoding Encoding => Encoding.UTF8;

        public Eff<RT, EnvironmentIO> EnvironmentEff => Eff<RT, EnvironmentIO>(rt => rt.Env.Environment);

        public Eff<RT, FileIO> FileEff => Eff<TestRuntime, FileIO>(rt => rt.Env.File);

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
        EnvironmentIO environment,
        FileIO file,
        IHostNetworkCommands<RT> hostNetworkCommands,
        ILoggerFactory loggerFactory,
        IPowershellEngine powershellEngine,
        ProcessRunnerIO processRunner,
        RegistryIO registry)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        public EnvironmentIO Environment { get; } = environment;

        public FileIO File { get; } = file;

        public IHostNetworkCommands<RT> HostNetworkCommands { get; } = hostNetworkCommands;

        public ILoggerFactory LoggerFactory { get; } = loggerFactory;

        public IPowershellEngine PowershellEngine { get; } = powershellEngine;

        public ProcessRunnerIO ProcessRunner { get; } = processRunner;

        public RegistryIO Registry { get; } = registry;
    }
}

