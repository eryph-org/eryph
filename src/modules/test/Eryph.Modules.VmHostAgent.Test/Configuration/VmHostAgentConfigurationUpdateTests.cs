using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Configuration;
using LanguageExt.Sys.Traits;
using Moq;

namespace Eryph.Modules.VmHostAgent.Test.Configuration;

using RT = VmHostAgentConfigurationTests.TestRuntime;
using static VmHostAgentConfigurationUpdate<VmHostAgentConfigurationTests.TestRuntime>;

public class VmHostAgentConfigurationUpdateTests
{
    private readonly RT _runtime;

    private readonly Mock<DirectoryIO> _directoryMock = new();
    private readonly Mock<FileIO> _fileMock = new();

    private const string ConfigPath = @"Z:\configs\config.yml";

    private readonly HostSettings _hostSettings = new()
    {
        DefaultDataPath = @"Y:\defaults\vms",
        DefaultVirtualHardDiskPath = @"Y:\defaults\disks\",
    };

    public VmHostAgentConfigurationUpdateTests()
    {
        _runtime = new(new(
            new CancellationTokenSource(),
            _directoryMock.Object,
            _fileMock.Object));
    }

    [Fact]
    public async Task UpdateConfig_MalformedConfig_ReturnsFail()
    {
        var result = await updateConfig("not a config", ConfigPath, _hostSettings).Run(_runtime);
        
        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration is malformed.");
    }

    [Fact]
    public async Task UpdateConfig_InvalidConfig_ReturnsFail()
    {
        var result = await updateConfig("not a config", ConfigPath, _hostSettings).Run(_runtime);

        result.Should().BeFail()
            .Which.Message.Should().Be("The configuration is malformed.");

        _fileMock.Verify(f => f.WriteAllText(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Encoding>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateConfig_ValidConfig_UpdatesConfig()
    {
        string yaml = """
                      defaults:
                        vms:
                        volumes:
                      """;

        var result = await updateConfig(yaml, ConfigPath, _hostSettings).Run(_runtime);

        result.Should().BeSuccess();

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

        _fileMock.VerifyNoOtherCalls();
    }
}
