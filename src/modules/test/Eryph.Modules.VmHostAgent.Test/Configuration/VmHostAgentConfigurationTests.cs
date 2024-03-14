using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using Eryph.Modules.VmHostAgent.Configuration;
using LanguageExt.Sys.Traits;
using LanguageExt;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Configuration;

using RT = VmHostAgentConfigurationTests.TestRuntime;
using static VmHostAgentConfiguration<VmHostAgentConfigurationTests.TestRuntime>;

public class VmHostAgentConfigurationTests
{
    private readonly RT _runtime;

    private readonly Mock<DirectoryIO> _directoryMock = new();
    private readonly Mock<FileIO> _fileMock = new();

    private const string ConfigPath = @"Z:\configs\config.yaml";

    private readonly HostSettings _hostSettings = new()
    {
        DefaultDataPath = @"Y:\defaults\vms",
        DefaultVirtualHardDiskPath = @"Y:\defaults\disks",
    };

    public VmHostAgentConfigurationTests()
    {
        _runtime = new(new(
            new CancellationTokenSource(),
            _directoryMock.Object,
            _fileMock.Object));
    }

    [Fact]
    public async Task GetConfigYaml_ConfigWithHyperVDefaultPaths_ReturnsYamlWithPath()
    {
        _fileMock.Setup(m => m.Exists(ConfigPath))
            .Returns(true)
            .Verifiable();

        _fileMock.Setup(m => m.ReadAllText(ConfigPath, Encoding.UTF8, It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                          defaults:
                            vms:
                            volumes:
                          """)
            .Verifiable();

        var result = await getConfigYaml(ConfigPath, _hostSettings).Run(_runtime);

        result.Should().BeSuccess().Which.Should().Be(
            $$"""
             defaults:
               vms: {{_hostSettings.DefaultDataPath}}
               volumes: {{_hostSettings.DefaultVirtualHardDiskPath}}
             datastores: 
             environments: 
             
             """);

        _fileMock.VerifyAll();
        _fileMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ParseConfig_EmptyConfig_ReturnsFail(string yaml)
    {
        var result = parseConfigYaml(yaml).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration must not be empty.");
    }

    [Fact]
    public void ParseConfig_MalformedConfig_ReturnsFail()
    {
        var result = parseConfigYaml("not a config").Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration is malformed.");
    }

    [Fact]
    public async Task ReadConfig_ConfigDoesNotExist_WritesAndReturnsDefaultConfig()
    {
        _fileMock.Setup(m => m.Exists(ConfigPath))
            .Returns(false)
            .Verifiable();

        var result = await readConfig(ConfigPath, _hostSettings).Run(_runtime);

        var config = result.Should().BeSuccess().Subject;
        config.Defaults.Vms.Should().Be(_hostSettings.DefaultDataPath);
        config.Defaults.Volumes.Should().Be(_hostSettings.DefaultVirtualHardDiskPath);
        config.Datastores.Should().BeNull();
        config.Environments.Should().BeNull();

        _fileMock.Verify(f => f.WriteAllText(
                ConfigPath,
                It.Is(
                    """
                    defaults:
                      vms: 
                      volumes: 
                    datastores: 
                    environments: 
                    
                    """,
                    EqualityComparer<string>.Default),
                Encoding.UTF8,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _fileMock.VerifyAll();
        _fileMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadConfig_ConfigWithoutDefaultPaths_ReturnsConfigWithSystemDefaults()
    {
        _fileMock.Setup(m => m.Exists(ConfigPath))
            .Returns(true)
            .Verifiable();

        _fileMock.Setup(m => m.ReadAllText(ConfigPath, Encoding.UTF8, It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                          defaults:
                            vms:
                            volumes:
                          """)
            .Verifiable();

        var result = await readConfig(ConfigPath, _hostSettings).Run(_runtime);

        var config = result.Should().BeSuccess().Subject;
        config.Defaults.Vms.Should().Be(_hostSettings.DefaultDataPath);
        config.Defaults.Volumes.Should().Be(_hostSettings.DefaultVirtualHardDiskPath);
        config.Datastores.Should().BeNull();
        config.Environments.Should().BeNull();

        _fileMock.VerifyAll();
        _fileMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadConfig_ConfigWithDefaultPaths_ReturnsConfig()
    {
        _fileMock.Setup(m => m.Exists(ConfigPath))
            .Returns(true)
            .Verifiable();

        _fileMock.Setup(m => m.ReadAllText(ConfigPath, Encoding.UTF8, It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                          defaults:
                            vms: Z:\test\vms
                            volumes: Z:\test\volumes
                          """)
            .Verifiable();

        var result = await readConfig(ConfigPath, _hostSettings).Run(_runtime);

        var config = result.Should().BeSuccess().Subject;
        config.Defaults.Vms.Should().Be(@"Z:\test\vms");
        config.Defaults.Volumes.Should().Be(@"Z:\test\volumes");
        config.Datastores.Should().BeNull();
        config.Environments.Should().BeNull();

        _fileMock.VerifyAll();
        _fileMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveConfig_ConfigWithHyperVDefaultPaths_SavesConfigWithoutPaths()
    {
        var config = new VmHostAgentConfiguration()
        {
            Defaults = new()
            {
                Vms = _hostSettings.DefaultDataPath,
                Volumes = _hostSettings.DefaultVirtualHardDiskPath,
            },
        };

        var result = await saveConfig(config, ConfigPath, _hostSettings).Run(_runtime);

        result.Should().BeSuccess();

        _directoryMock.Verify(m => m.Create(@"Z:\configs"), Times.Once);
        _directoryMock.VerifyNoOtherCalls();

        _fileMock.Verify(m => m.WriteAllText(
                ConfigPath,
                It.Is(
                    """
                    defaults:
                      vms: 
                      volumes: 
                    datastores: 
                    environments: 
                    
                    """,
                    EqualityComparer<string>.Default),
                Encoding.UTF8,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fileMock.VerifyNoOtherCalls();
    }

    public readonly struct TestRuntime(TestRuntimeEnv env) :
        HasFile<RT>,
        HasDirectory<RT>
    {
        public readonly TestRuntimeEnv Env = env;

        public RT LocalCancel => new(new TestRuntimeEnv(
            new CancellationTokenSource(),
            Env.Directory,
            Env.File));

        public CancellationToken CancellationToken => Env.CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => Env.CancellationTokenSource;

        public Eff<RT, DirectoryIO> DirectoryEff => Eff<RT, DirectoryIO>(rt => rt.Env.Directory);

        public Encoding Encoding => Encoding.UTF8;

        public Eff<RT, FileIO> FileEff => Eff<RT, FileIO>(rt => rt.Env.File);
    }

    public class TestRuntimeEnv(
        CancellationTokenSource cancellationTokenSource,
        DirectoryIO directory,
        FileIO file)
    {
        public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
        
        public DirectoryIO Directory { get; } = directory;

        public FileIO File { get; } = file;
    }
}
