using System.Text;
using Eryph.Core.VmAgent;
using Eryph.Modules.VmHostAgent.Configuration;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test.Configuration;

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
        DefaultVirtualHardDiskPath = @"Y:\defaults\disks\",
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
               watch_file_system: true
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
        var result = parseConfigYaml(yaml, false).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration must not be empty.");
    }

    [Fact]
    public void ParseConfig_MalformedConfig_ReturnsFail()
    {
        var result = parseConfigYaml("not a config", false).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration is malformed.");
    }

    [Fact]
    public void ParseConfig_UndefinedPropertyWhenUsingStrictMode_ReturnsFail()
    {
        var yaml = """
                   datastores:
                   - name: teststore
                     path: Z:\teststore
                   undefined_property: test
                   """;
        var result = parseConfigYaml(yaml, true).Run();

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration is malformed.");
    }

    [Fact]
    public void ParseConfig_UndefinedPropertyWhenNotUsingStrictMode_ReturnsConfig()
    {
        var yaml = """
                   datastores:
                   - name: teststore
                     path: Z:\teststore
                   undefined_property: test
                   """;
        var result = parseConfigYaml(yaml, false).Run();

        result.Should().BeSuccess().Which.Datastores.Should().SatisfyRespectively(
            ds =>
            {
                ds.Name.Should().Be("teststore");
                ds.Path.Should().Be(@"Z:\teststore");
            });
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
                      watch_file_system: true
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
        config.Defaults.WatchFileSystem.Should().BeTrue();
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
        config.Defaults.WatchFileSystem.Should().BeTrue();
        config.Datastores.Should().BeNull();
        config.Environments.Should().BeNull();

        _fileMock.VerifyAll();
        _fileMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task ReadConfig_ConfigWithWatcherSettings_ReturnsConfig(
        string actual,
        bool expected)
    {
        _fileMock.Setup(m => m.Exists(ConfigPath))
            .Returns(true)
            .Verifiable();

        _fileMock.Setup(m => m.ReadAllText(ConfigPath, Encoding.UTF8, It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""
                          defaults:
                            vms: Z:\defaults\vms
                            volumes: Z:\defaults\volumes
                            watch_file_system: {{ actual }}
                          datastores:
                          - name: store1
                            path: Z:\stores\store1
                            watch_file_system: {{ actual }}
                          environments:
                          - name: env1
                            defaults:
                              vms: Z:\env1\vms
                              volumes: Z:\env1\volumes
                              watch_file_system: {{ actual }}
                            datastores:
                            - name: store1
                              path: Z:\env1\stores\store1
                              watch_file_system: {{ actual }}
                          """)
            .Verifiable();

        var result = await readConfig(ConfigPath, _hostSettings).Run(_runtime);

        var config = result.Should().BeSuccess().Subject;
        config.Defaults.Vms.Should().Be(@"Z:\defaults\vms");
        config.Defaults.Volumes.Should().Be(@"Z:\defaults\volumes");
        config.Defaults.WatchFileSystem.Should().Be(expected);
        config.Datastores.Should().SatisfyRespectively(
            datastore =>
            {
                datastore.Name.Should().Be("store1");
                datastore.Path.Should().Be(@"Z:\stores\store1");
                datastore.WatchFileSystem.Should().Be(expected);
            });
        config.Environments.Should().SatisfyRespectively(
            environment =>
            {
                environment.Defaults.Vms.Should().Be(@"Z:\env1\vms");
                environment.Defaults.Volumes.Should().Be(@"Z:\env1\volumes");
                environment.Defaults.WatchFileSystem.Should().Be(expected);
                environment.Datastores.Should().SatisfyRespectively(
                    datastore =>
                    {
                        datastore.Name.Should().Be("store1");
                        datastore.Path.Should().Be(@"Z:\env1\stores\store1");
                        datastore.WatchFileSystem.Should().Be(expected);
                    });
            });

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
                Vms = @"Y:\defaults\vms\",
                Volumes = @"Y:\defaults\disks",
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
                      watch_file_system: true
                    datastores: 
                    environments: 
                    
                    """,
                    EqualityComparer<string>.Default),
                Encoding.UTF8,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fileMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveConfig_ConfigWithUnnormalizedPaths_SavesConfigWithNormalizedPaths()
    {
        var config = new VmHostAgentConfiguration()
        {
            Defaults = new()
            {
                Vms = @"Z:\defaults\vms\",
                Volumes = @"Z:\defaults\volumes\",
            },
            Datastores =
            [
                new VmHostAgentDataStoreConfiguration()
                {
                    Name = "store1",
                    Path = @"Z:\stores\store1\",
                }
            ],
            Environments = [
                new VmHostAgentEnvironmentConfiguration()
                {
                    Name = "env1",
                    Defaults = new VmHostAgentDefaultsConfiguration()
                    {
                        Vms = @"Z:\env1\vms\",
                        Volumes = @"Z:\env1\volumes\",
                    },
                    Datastores =
                    [
                        new VmHostAgentDataStoreConfiguration()
                        {
                            Name = "store1",
                            Path = @"Z:\env1\stores\store1\",

                        },
                    ],
                },
            ],
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
                      vms: Z:\defaults\vms
                      volumes: Z:\defaults\volumes
                      watch_file_system: true
                    datastores:
                    - name: store1
                      path: Z:\stores\store1
                      watch_file_system: true
                    environments:
                    - name: env1
                      defaults:
                        vms: Z:\env1\vms
                        volumes: Z:\env1\volumes
                        watch_file_system: true
                      datastores:
                      - name: store1
                        path: Z:\env1\stores\store1
                        watch_file_system: true

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
