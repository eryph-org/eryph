using System.Net;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.StateDb.Specifications;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit.Abstractions;

using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.HostAgent.HyperV.Test.TestRuntime>;
using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdateInConsole<Eryph.Modules.HostAgent.HyperV.Test.TestRuntime>;
using ChangeOp = Eryph.Modules.HostAgent.Networks.NetworkChangeOperation<Eryph.Modules.HostAgent.HyperV.Test.TestRuntime>;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class ProviderNetworkConsoleTests
{
    private readonly Mock<IOVSControl> _ovsControlMock = new();
    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();
    private readonly Mock<IHostNetworkCommands<TestRuntime>> _hostNetworkCommandsMock = new();
    private readonly Mock<ISyncClient> _syncClientMock = new();
    private readonly TestRuntime _runtime;
    private readonly ITestOutputHelper _testOutput;

    public ProviderNetworkConsoleTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _runtime = TestRuntime.New(
            _ovsControlMock.Object,
            _syncClientMock.Object,
            _hostNetworkCommandsMock.Object,
            _networkProviderManagerMock.Object);
    }

    [Fact]
    public async Task Sync_Before_new_config_happy_path()
    {
        var hostState = CreateHostState();
        AddMocks();
            
        var changes = new NetworkChanges<TestRuntime>
        {
            Operations = new[]
            {
                new ChangeOp(
                    NetworkChangeOperation.AddBridge,
                    () => unitAff, null, null, false)
            }.ToSeq()
        };

        _runtime.Env.AnsiConsole.Input.PushTextWithEnter("a");

        var fin = await syncCurrentConfigBeforeNewConfig(hostState, changes, false, () => SuccessAff(hostState))
            .Run(_runtime);

        var result = fin.Should().BeSuccess().Subject;
        result.IsValid.Should().BeTrue();

        var generatedText = _runtime.Env.AnsiConsole.Lines;
        _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_happy_path)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(13);
        generatedText[3].Should().Match("  Add bridge '{0}'*");
        generatedText[12].Should().Be("Host network configuration was updated.");
    }

    [Fact]
    public async Task Sync_Before_new_config_with_rollback()
    {
        var hostState = CreateHostState();

        var rolledBack = false;

        var changes = new NetworkChanges<TestRuntime>
        {
            Operations = new[]
            {
                new ChangeOp(
                    NetworkChangeOperation.AddBridge,
                    () => unitAff, _ => true , () =>
                    {
                        rolledBack = true;
                        return unitAff;
                    },
                    false),
                new ChangeOp(
                    NetworkChangeOperation.RebuildOverLaySwitch,
                    () => unitAff, null, null, false),
                new ChangeOp(
                    NetworkChangeOperation.AddNetNat,
                    () => FailAff<Unit>(Errors.TimedOut), null, null, false)
            }.ToSeq()
        };

        _runtime.Env.AnsiConsole.Input.PushTextWithEnter("a");

        var fin = await syncCurrentConfigBeforeNewConfig(hostState, changes, false, () => SuccessAff(hostState))
            .Run(_runtime);

        fin.Should().BeFail().Which.Should().Be(Errors.TimedOut);
        rolledBack.Should().BeTrue();

        var generatedText = _runtime.Env.AnsiConsole.Lines;
        _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_with_rollback)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(23);
        generatedText[5].Should().Match("  Add host NAT for provider '{0}' with prefix '{1}'*");
        generatedText[18].Should().Match("  Rollback of Add bridge '{0}'...*");
    }

    [Fact]
    public async Task Apply_new_config_happy_path()
    {
        var hostState = CreateHostState();
        AddMocks();

        var changes = new NetworkChanges<TestRuntime>
        {
            Operations = new[]
            {
                new ChangeOp(
                    NetworkChangeOperation.AddBridge,
                    () => unitAff, null, null, false)
            }.ToSeq()
        };

        _runtime.Env.AnsiConsole.Input.PushTextWithEnter("a");

        var fin = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => applyChangesInConsole(changes, () => SuccessAff(hostState), false, c))
            .Run(_runtime);

        fin.Should().BeSuccess();

        var generatedText = _runtime.Env.AnsiConsole.Lines;
        _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_happy_path)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(10);
        generatedText[2].Should().Match("  Add bridge '{0}'*");
    }

    [Fact]
    public async Task Apply_new_config_with_rollback()
    {
        var hostState = CreateHostState();
        AddMocks();

        var rolledBack = false;

        var changes = new NetworkChanges<TestRuntime>
        {
            Operations = new[]
            {
                new ChangeOp(
                    NetworkChangeOperation.AddBridge,
                    () => unitAff, _ => true , () =>
                    {
                        rolledBack = true;
                        return unitAff;
                    },
                    false),
                new ChangeOp(
                    NetworkChangeOperation.RebuildOverLaySwitch,
                    () => unitAff, null, null, false),
                new ChangeOp(
                    NetworkChangeOperation.AddNetNat,
                    () => FailAff<Unit>(Errors.TimedOut), null, null, false)
            }.ToSeq()
        };

        _runtime.Env.AnsiConsole.Input.PushTextWithEnter("a");

        var fin = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
                .Bind(c => applyChangesInConsole(changes, () => SuccessAff(hostState), false, c))
                .Run(_runtime);

        fin.Should().BeFail().Which.Should().Be(Errors.TimedOut);
        rolledBack.Should().BeTrue();

        var generatedText = _runtime.Env.AnsiConsole.Lines;
        _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_with_rollback)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(25);
        generatedText[2].Should().Match("  Add bridge '{0}'*");
        generatedText[15].Should().Match("  Rollback of Add bridge '{0}'...*");
        generatedText[23].Should().Match("  Update mapping of bridges to network providers...*");
    }

    private static HostState CreateHostState()
    {
        var switchId = Guid.NewGuid();

        var hostState = new HostState(
            Seq1(new VMSwitchExtension
            {
                Enabled = true,
                Id = Guid.NewGuid().ToString(),
                SwitchId = switchId,
                SwitchName = EryphConstants.OverlaySwitchName
            }),
            Seq1(new VMSwitch
            {
                Id = switchId,
                Name = EryphConstants.OverlaySwitchName,
                NetAdapterInterfaceGuid = null
            }),
            new HostAdaptersInfo(HashMap(
                ("Ethernet", new HostAdapterInfo("Ethernet", Guid.NewGuid(), None, true)))),
            Seq1(new NetNat { Name = "docker_nat", InternalIPInterfaceAddressPrefix = "192.168.10.0/24" }),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        return hostState;
    }

    private void AddMocks()
    {
        _hostNetworkCommandsMock.Setup(x => x.GetNetAdaptersBySwitch(It.IsAny<Guid>()))
            .Returns(SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));

        _ovsControlMock.Setup(x => x.GetOVSTable(It.IsAny<CancellationToken>()))
            .Returns(new OVSTableRecord());

        _ovsControlMock.Setup(x => x.AddBridge("br-nat",
                It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, Unit>(unit));

        _ovsControlMock.Setup(x => x.UpdateBridgePort("br-nat",
                None, None,
                It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, Unit>(unit));

        _hostNetworkCommandsMock.Setup(x => x.WaitForBridgeAdapter(
                It.IsAny<string>()))
            .Returns(unitAff);
        _hostNetworkCommandsMock.Setup(x => x.EnableBridgeAdapter(
                It.IsAny<string>()))
            .Returns(unitAff);

        _hostNetworkCommandsMock.Setup(x => x.GetAdapterIpV4Address("br-nat"))
            .Returns(SuccessAff(Seq<NetIpAddress>()));

        _hostNetworkCommandsMock.Setup(x => x.ConfigureAdapterIp("br-nat",
                It.IsAny<IPAddress>(), It.IsAny<IPNetwork2>()))
            .Returns(unitAff);

        _hostNetworkCommandsMock.Setup(x => x.AddNetNat("eryph_default_default",
                It.IsAny<IPNetwork2>()))
            .Returns(unitAff);

        _syncClientMock.Setup(x => x.CheckRunning(It.IsAny<CancellationToken>()))
            .Returns(SuccessAff(true));

        _ovsControlMock.Setup(x => x.UpdateBridgeMapping(It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(RightAsync<Error, Unit>(unit));
    }
}
