﻿using System.Net;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdateInConsole<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using ChangeOp = Eryph.Modules.VmHostAgent.Networks.NetworkChangeOperation<Eryph.Modules.VmHostAgent.Test.TestRuntime>;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test
{
    public class ProviderNetworkConsoleTests
    {
        private readonly ITestOutputHelper _testOutput;

        public ProviderNetworkConsoleTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task GetHostState_Gets_Expected_state()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();
            AddMocks(runtime, hostState);


            var res = (await getHostState()
                    .Run(runtime));

            res.Match(
                Fail: l => l.Throw(),
                Succ: newState =>
                {
                    newState.NetAdapters.Should().HaveCount(1);
                    newState.VMSwitchExtensions.Should().HaveCount(1);
                    newState.VMSwitches.Should().HaveCount(1);
                    newState.NetNat.Should().HaveCount(1);
                    newState.OvsBridges.Should().BeEmpty();
                    newState.OvsBridgePorts.Should().BeEmpty();

                }
            );
        }

        [Fact]
        public async Task GenerateChanges_generates_as_expected()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();
            AddMocks(runtime, hostState);

            var res = (await importConfig(NetworkProvidersConfiguration.DefaultConfig)
                .Bind(c => generateChanges(hostState, c))
                .Run(runtime));

            res.Match(
                Fail: l => l.Throw(),
                Succ: (changes) =>
                {
                    changes.Operations.Should().HaveCount(4);
                    changes.Operations.Select(x => x.Operation)
                        .Should().ContainInOrder(
                            NetworkChangeOperation.AddBridge,
                            NetworkChangeOperation.ConfigureNatIp,
                            NetworkChangeOperation.AddNetNat,
                            NetworkChangeOperation.UpdateBridgeMapping
                        );
                });

        }

        [Fact]
        public async Task GenerateChanges_multiple_overlay_switches_triggers_rebuild()
        {
            static HostState CreateHostStateWithMultipleSwitches()
            {
                var hostState = CreateHostState();
                return hostState with
                {
                    VMSwitches = Prelude.Seq(
                    [
                        ..hostState.VMSwitches,
                        new VMSwitch()
                        {
                            Id = Guid.NewGuid(),
                            Name = EryphConstants.OverlaySwitchName,
                        }
                    ]),
                };
            }

            var runtime = TestRuntime.New();
            var hostState = CreateHostStateWithMultipleSwitches();
            AddMocks(runtime, hostState);

            var res = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
                .Bind(c => generateChanges(hostState, c))
                .Run(runtime);

            var operations = res.Should().BeSuccess().Which.Operations;
            operations.Should().SatisfyRespectively(
                op => op.Operation.Should().Be(NetworkChangeOperation.StopOVN),
                op => op.Operation.Should().Be(NetworkChangeOperation.RebuildOverLaySwitch),
                op => op.Operation.Should().Be(NetworkChangeOperation.StartOVN),
                op => op.Operation.Should().Be(NetworkChangeOperation.AddBridge),
                op => op.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
                op => op.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
                op => op.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
        }

        [Fact]
        public async Task GenerateChanges_invalid_NetNat_recreates_NetNat()
        {
            static HostState CreateHostStateWithMultipleSwitches()
            {
                var hostState = CreateHostState();
                return hostState with
                {
                    NetNat = Prelude.Seq(
                    [
                        ..hostState.NetNat,
                        new NetNat()
                        {
                            Name = "eryph_default_default",
                            InternalIPInterfaceAddressPrefix = "10.249.248.0/28",
                        }
                    ]),
                };
            }

            var runtime = TestRuntime.New();
            var hostState = CreateHostStateWithMultipleSwitches();
            AddMocks(runtime, hostState);

            var res = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
                .Bind(c => generateChanges(hostState, c))
                .Run(runtime);

            var operations = res.Should().BeSuccess().Which.Operations;
            operations.Should().SatisfyRespectively(
                op => op.Operation.Should().Be(NetworkChangeOperation.AddBridge),
                op => op.Operation.Should().Be(NetworkChangeOperation.RemoveNetNat),
                op => op.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
                op => op.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
                op => op.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
        }

        [Fact]
        public async Task Sync_Before_new_config_happy_path()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();
            AddMocks(runtime, hostState);
            
            var changes = new NetworkChanges<TestRuntime>
            {
                Operations = new[]
                {
                    new NetworkChangeOperation<TestRuntime>(
                        NetworkChangeOperation.AddBridge,
                        () => Prelude.unitAff, null, null)
                }.ToSeq()
            };

            runtime.Env.Console.WriteKeyLine("a");


            var res = (await syncCurrentConfigBeforeNewConfig(hostState, changes, false)
                .Run(runtime));

            res.Match(
                Fail: l => l.Throw(),
                Succ: (r) =>
                {
                    r.IsValid.Should().BeTrue();
                    r.HostState.Should().BeEquivalentTo(hostState);
                    r.HostState.Should().NotBeSameAs(hostState);

                    var generatedText = string.Join('\n', runtime.Env.Console.ToList()).Split("\n");
                    _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_happy_path)}:");
                    generatedText.Iter(_testOutput.WriteLine);

                    generatedText.Should().HaveCount(16);
                    generatedText[2].Should().Be("- Add bridge '{0}'");
                    generatedText[13].Should().Be("Host network configuration was updated.");

                });

        }


        [Fact]
        public async Task Sync_Before_new_config_with_rollback()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();

            var rolledBack = false;

            var changes = new NetworkChanges<TestRuntime>
            {
                Operations = new[]
                {
                    new ChangeOp(
                        NetworkChangeOperation.AddBridge,
                        () => Prelude.unitAff, _ => true , () =>
                        {
                            rolledBack = true;
                            return Prelude.unitAff;
                        }),
                    new ChangeOp(
                        NetworkChangeOperation.RebuildOverLaySwitch,
                        () => Prelude.unitAff, null, null),

                    new ChangeOp(
                        NetworkChangeOperation.AddNetNat,
                        () => Prelude.FailAff<Unit>(Errors.TimedOut), null, null)
                }.ToSeq()
            };

            runtime.Env.Console.WriteKeyLine("a");

            var res = (await syncCurrentConfigBeforeNewConfig(hostState, changes, false)
                .Run(runtime));

            res.Match(
                Fail: l =>
                {
                    Assert.Same(Errors.TimedOut, l);
                },
                Succ: (_) => throw new Exception("This should not succeed!"));

            Assert.True(rolledBack);

            var generatedText = string.Join('\n', runtime.Env.Console.ToList()).Split("\n");
            _testOutput.WriteLine($"Generated Console output of {nameof(Sync_Before_new_config_with_rollback)}:");
            generatedText.Iter(_testOutput.WriteLine);

            generatedText.Should().HaveCount(22);
            generatedText[4].Should().Be("- Add host NAT for provider '{0}' with prefix '{1}'");
            generatedText[17].Should().Be("rollback of: Add bridge '{0}'");

        }

        [Fact]
        public async Task Apply_new_config_happy_path()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();
            AddMocks(runtime, hostState);

            var changes = new NetworkChanges<TestRuntime>
            {
                Operations = new[]
                {
                    new ChangeOp(
                        NetworkChangeOperation.AddBridge,
                        () => Prelude.unitAff, null, null)
                }.ToSeq()
            };

            runtime.Env.Console.WriteKeyLine("a");



            var res = await
                importConfig(NetworkProvidersConfiguration.DefaultConfig)
                    .Bind(c => applyChangesInConsole(c, changes, false, true))
                    .Run(runtime);

            res.Match(
                Fail: l => l.Throw(),
                Succ: (_) =>
                {
                    var generatedText = string.Join('\n', runtime.Env.Console.ToList()).Split("\n");
                    _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_happy_path)}:");
                    generatedText.Iter(_testOutput.WriteLine);

                    generatedText.Should().HaveCount(14);
                    generatedText[2].Should().Be("- Add bridge '{0}'");

                });

        }

        [Fact]
        public async Task Apply_new_config_with_rollback()
        {
            var runtime = TestRuntime.New();
            var hostState = CreateHostState();
            AddMocks(runtime, hostState);

            var rolledBack = false;

            var changes = new NetworkChanges<TestRuntime>
            {
                Operations = new[]
                {
                    new ChangeOp(
                        NetworkChangeOperation.AddBridge,
                        () => Prelude.unitAff, _ => true , () =>
                        {
                            rolledBack = true;
                            return Prelude.unitAff;
                        }),
                    new ChangeOp(
                        NetworkChangeOperation.RebuildOverLaySwitch,
                        () => Prelude.unitAff, null, null),

                    new ChangeOp(
                        NetworkChangeOperation.AddNetNat,
                        () => Prelude.FailAff<Unit>(Errors.TimedOut), null, null)
                }.ToSeq()
            };

            runtime.Env.Console.WriteKeyLine("a");

            var res = await
                importConfig(NetworkProvidersConfiguration.DefaultConfig)
                    .Bind(c => applyChangesInConsole(c, changes, false, true))
                    .Run(runtime);
            res.Match(
                Fail: l =>
                {
                    Assert.Same(Errors.TimedOut, l);
                },
                Succ: (_) => throw new Exception("This should not succeed!"));

            Assert.True(rolledBack);

            var generatedText = string.Join('\n', runtime.Env.Console.ToList()).Split("\n");
            _testOutput.WriteLine($"Generated Console output of {nameof(Apply_new_config_with_rollback)}:");
            generatedText.Iter(_testOutput.WriteLine);

            generatedText.Should().HaveCount(32);
            generatedText[2].Should().Be("- Add bridge '{0}'");
            generatedText[16].Should().Be("rollback of: Add bridge '{0}'");
            generatedText[27].Should().Contain("running: Update mapping of bridges to network providers");

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
                Seq1(new HostNetworkAdapter
                {
                    Name = "Ethernet",
                    InterfaceGuid = Guid.NewGuid()
                }),
                Seq1("Ethernet"),
                Some(new OverlaySwitchInfo(switchId, HashSet<string>())),
                Seq1(new NetNat { Name = "docker_nat", InternalIPInterfaceAddressPrefix = "192.168.10.0/24" }),
                Seq<Bridge>(),
                Seq<BridgePort>(),
                Seq<Interface>());

            return hostState;
        }

        private static void AddMocks(TestRuntime runtime, 
            HostState hostState, Seq<TypedPsObject<VMNetworkAdapter>> 
                vmAdaptersInOverlaySwitch = default)
        {
            var syncClientMock = new Mock<ISyncClient>();
            var ovsControlMock = new Mock<IOVSControl>();
            var hostCommandsMock = new Mock<IHostNetworkCommands<TestRuntime>>();
            var configManager = new Mock<INetworkProviderManager>();


            runtime.Env.OVS = ovsControlMock.Object;
            runtime.Env.SyncClient = syncClientMock.Object;
            runtime.Env.HostNetworkCommands = hostCommandsMock.Object;
            runtime.Env.NetworkProviderManager = configManager.Object;

            var realConfigManager = new NetworkProviderManager();

            configManager.Setup(x => x.ParseConfigurationYaml(It.IsAny<string>()))
                .Returns(realConfigManager.ParseConfigurationYaml);

            var overlaySwitch = hostState.VMSwitches
                .FirstOrDefault(x => x.Name == EryphConstants.OverlaySwitchName);

            hostCommandsMock.Setup(x => x.GetNetAdaptersBySwitch(It.IsAny<Guid>()))
                .Returns(Prelude.SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));
            if (overlaySwitch != null)
            {
                hostCommandsMock.Setup(x =>
                        x.GetNetAdaptersBySwitch(overlaySwitch.Id))
                    .Returns(Prelude.SuccessAff(vmAdaptersInOverlaySwitch));
            }


            hostCommandsMock.Setup(x => x.GetSwitchExtensions())
                .Returns(Prelude.SuccessAff(hostState.VMSwitchExtensions));

            hostCommandsMock.Setup(x => x.GetSwitchExtensions())
                .Returns(Prelude.SuccessAff(hostState.VMSwitchExtensions));
            hostCommandsMock.Setup(x => x.GetSwitches())
                .Returns(Prelude.SuccessAff(hostState.VMSwitches));
            hostCommandsMock.Setup(x => x.GetPhysicalAdapters())
                .Returns(Prelude.SuccessAff(hostState.NetAdapters));
            hostCommandsMock.Setup(x => x.GetAdapterNames())
                .Returns(Prelude.SuccessAff(hostState.NetAdapters.Select(x=>x.Name)));
            hostCommandsMock.Setup(x => x.FindOverlaySwitch(
                    It.IsAny<Seq<VMSwitch>>(),
                    It.IsAny<Seq<HostNetworkAdapter>>()))
                .Returns(Prelude.SuccessAff(hostState.OverlaySwitch));

            hostCommandsMock.Setup(x => x.GetNetNat())
                .Returns(Prelude.SuccessAff(hostState.NetNat));

            ovsControlMock.Setup(x => x.GetBridges(CancellationToken.None))
                .Returns(hostState.OvsBridges);
            ovsControlMock.Setup(x => x.GetPorts(CancellationToken.None))
                .Returns(hostState.OvsBridgePorts);

            ovsControlMock.Setup(x => x.GetOVSTable(It.IsAny<CancellationToken>()))
                .Returns(new OVSTableRecord());

            ovsControlMock.Setup(x => x.GetBridges(It.IsAny<CancellationToken>()))
                .Returns(hostState.OvsBridges);

            ovsControlMock.Setup(x => x.GetPorts(It.IsAny<CancellationToken>()))
                .Returns(hostState.OvsBridgePorts);


            ovsControlMock.Setup(x => x.AddBridge("br-nat",
                    It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, Unit>(Prelude.unit));

            ovsControlMock.Setup(x => x.UpdateBridgePort("br-nat",
                    null,null,
                    It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, Unit>(Prelude.unit));


            hostCommandsMock.Setup(x => x.WaitForBridgeAdapter(
                    It.IsAny<string>()))
                .Returns(Prelude.unitAff);
            hostCommandsMock.Setup(x => x.EnableBridgeAdapter(
                    It.IsAny<string>()))
                .Returns(Prelude.unitAff);

            hostCommandsMock.Setup(x => x.GetAdapterIpV4Address("br-nat"))
                .Returns(Prelude.SuccessAff(Seq<NetIpAddress>()));

            hostCommandsMock.Setup(x => x.ConfigureAdapterIp("br-nat",
                    It.IsAny<IPAddress>(), It.IsAny<IPNetwork2>()))
                .Returns(Prelude.unitAff);

            hostCommandsMock.Setup(x => x.AddNetNat("eryph_default_default",
                    It.IsAny<IPNetwork2>()))
                .Returns(Prelude.unitAff);

            syncClientMock.Setup(x => x.CheckRunning(It.IsAny<CancellationToken>()))
                .Returns(Prelude.SuccessAff(true));

            ovsControlMock.Setup(x => x.UpdateBridgeMapping(It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Prelude.RightAsync<Error, Unit>(Prelude.unit));


        }

    }
}
