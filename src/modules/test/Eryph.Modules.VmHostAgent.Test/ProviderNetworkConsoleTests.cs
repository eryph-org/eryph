﻿using System.Net;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit.Abstractions;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdateInConsole<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using ChangeOp = Eryph.Modules.VmHostAgent.Networks.NetworkChangeOperation<Eryph.Modules.VmHostAgent.Test.TestRuntime>;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test;

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

        _runtime.Env.Console.WriteKeyLine("a");


        var res = await syncCurrentConfigBeforeNewConfig(hostState, changes, false)
            .Run(_runtime);

        res.Match(
            Fail: l => l.Throw(),
            Succ: (r) =>
            {
                r.IsValid.Should().BeTrue();
                r.RefreshState.Should().BeTrue();

                var generatedText = string.Join('\n', _runtime.Env.Console.ToList()).Split("\n");
                _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_happy_path)}:");
                generatedText.Iter(_testOutput.WriteLine);

                generatedText.Should().HaveCount(15);
                generatedText[2].Should().Be("- Add bridge '{0}'");
                generatedText[13].Should().Be("Host network configuration was updated.");

            });
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

        _runtime.Env.Console.WriteKeyLine("a");

        var res = (await syncCurrentConfigBeforeNewConfig(hostState, changes, false)
            .Run(_runtime));

        res.Match(
            Fail: l =>
            {
                Assert.Same(Errors.TimedOut, l);
            },
            Succ: (_) => throw new Exception("This should not succeed!"));

        Assert.True(rolledBack);

        var generatedText = string.Join('\n', _runtime.Env.Console.ToList()).Split("\n");
        _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_with_rollback)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(22);
        generatedText[4].Should().Be("- Add host NAT for provider '{0}' with prefix '{1}'");
        generatedText[17].Should().Be("rollback of: Add bridge '{0}'");
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

        _runtime.Env.Console.WriteKeyLine("a");



        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => applyChangesInConsole(c, changes, _ => SuccessAff(hostState), false, true))
            .Run(_runtime);

        result.Match(
            Fail: l => l.Throw(),
            Succ: (_) =>
            {
                var generatedText = string.Join('\n', _runtime.Env.Console.ToList()).Split("\n");
                _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_happy_path)}:");
                generatedText.Iter(_testOutput.WriteLine);

                generatedText.Should().HaveCount(14);
                generatedText[2].Should().Be("- Add bridge '{0}'");
            });
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

        _runtime.Env.Console.WriteKeyLine("a");

        var res = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
                .Bind(c => applyChangesInConsole(c, changes, _ => SuccessAff(hostState), false, true))
                .Run(_runtime);
        res.Match(
            Fail: l =>
            {
                Assert.Same(Errors.TimedOut, l);
            },
            Succ: (_) => throw new Exception("This should not succeed!"));

        Assert.True(rolledBack);

        var generatedText = string.Join('\n', _runtime.Env.Console.ToList()).Split("\n");
        _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_with_rollback)}:");
        generatedText.Iter(_testOutput.WriteLine);

        generatedText.Should().HaveCount(31);
        generatedText[2].Should().Be("- Add bridge '{0}'");
        generatedText[16].Should().Be("rollback of: Add bridge '{0}'");
        generatedText[26].Should().Contain("running: Update mapping of bridges to network providers");
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
            new OvsBridgesInfo());

        return hostState;
    }

    private void AddMocks(
        Guid? switchId = null,
        Seq<TypedPsObject<VMNetworkAdapter>> vmAdaptersInOverlaySwitch = default)
    {
        _hostNetworkCommandsMock.Setup(x => x.GetNetAdaptersBySwitch(It.IsAny<Guid>()))
            .Returns(SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));
        if (switchId.HasValue)
        {
            _hostNetworkCommandsMock.Setup(x => x.GetNetAdaptersBySwitch(switchId.Value))
                .Returns(SuccessAff(vmAdaptersInOverlaySwitch));
        }

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
