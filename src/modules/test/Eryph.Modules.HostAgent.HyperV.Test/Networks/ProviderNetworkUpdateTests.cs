using System.Net;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using Moq;
using static Eryph.Modules.HostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.HostAgent.HyperV.Test.TestRuntime>;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.HyperV.Test.Networks;

public class ProviderNetworkUpdateTests
{
    private readonly Mock<IOVSControl> _ovsControlMock = new();
    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();
    private readonly Mock<IHostNetworkCommands<TestRuntime>> _hostNetworkCommandsMock = new();
    private readonly Mock<ISyncClient> _syncClientMock = new();
    private readonly TestRuntime _runtime;

    private static readonly Guid OverlaySwitchId = Guid.NewGuid();

    public ProviderNetworkUpdateTests()
    {
        _runtime = TestRuntime.New(
            _ovsControlMock.Object,
            _syncClientMock.Object,
            _hostNetworkCommandsMock.Object,
            _networkProviderManagerMock.Object);

        _ovsControlMock.Setup(x => x.GetOVSTable(It.IsAny<CancellationToken>()))
            .Returns(new OVSTableRecord());
    }

    [Fact]
    public void AddFallbackData_OldAdapterNameIsUsedInBridge_ReturnsFallbackData()
    {
        var pif1Id = Guid.NewGuid();
        var pif2Id = Guid.NewGuid();
        var pif3Id = Guid.NewGuid();

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap(
                ("pif-1", new HostAdapterInfo("pif-1", pif1Id, None, true, None)),
                ("pif-2", new HostAdapterInfo("pif-2", pif2Id, None, true, None)),
                ("pif-3", new HostAdapterInfo("pif-3", pif3Id, None, true, None)))),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap(
                ("br-pif", new OvsBridgeInfo("br-pif", HashMap(
                    ("br-pif-bond", new OvsBridgePortInfo(
                        "br-pif-bond", "br-pif", None, None, None, Seq(
                            new OvsInterfaceInfo("pif-1-old", "", None, None, pif1Id, "pif-1-old"),
                            new OvsInterfaceInfo("pif-3", "", None, None, pif2Id, "pif-3")))
                    ))
                ))
            )));

        var result = addFallbackData(hostState).Run();

        var resultHostState = result.Should().BeSuccess().Subject;

        resultHostState.HostAdapters.Adapters.Should().HaveCount(4);

        var pif1Info = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-1").WhoseValue;
        pif1Info.InterfaceId.Should().Be(pif1Id);
        pif1Info.Name.Should().Be("pif-1");
        pif1Info.ConfiguredName.Should().BeSome().Which.Should().Be("pif-1-old");
        pif1Info.IsPhysical.Should().BeTrue();

        var pif1OldInfo = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-1-old").WhoseValue;
        pif1OldInfo.InterfaceId.Should().Be(pif1Id);
        pif1OldInfo.Name.Should().Be("pif-1");
        pif1OldInfo.ConfiguredName.Should().BeSome().Which.Should().Be("pif-1-old");
        pif1OldInfo.IsPhysical.Should().BeTrue();

        var pif2Info = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-2").WhoseValue;
        pif2Info.InterfaceId.Should().Be(pif2Id);
        pif2Info.Name.Should().Be("pif-2");
        pif2Info.ConfiguredName.Should().Be("pif-3");
        pif2Info.IsPhysical.Should().BeTrue();

        // The fallback data must not override current adapter information
        var pif3Info = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-3").WhoseValue;
        pif3Info.InterfaceId.Should().Be(pif3Id);
        pif3Info.Name.Should().Be("pif-3");
        pif3Info.ConfiguredName.Should().BeNone();
        pif3Info.IsPhysical.Should().BeTrue();
    }

    [Fact]
    public void AddFallbackData_NewAdapterNameIsUsedInBridge_ReturnsFallbackData()
    {
        var pif1Id = Guid.NewGuid();

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap(
                ("pif-1", new HostAdapterInfo("pif-1", pif1Id, None, true, None)))),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap(
                ("br-pif", new OvsBridgeInfo("br-pif", HashMap(
                    ("pif-1", new OvsBridgePortInfo(
                        "pif-1", "br-pif", None, None, None, Seq1(
                            new OvsInterfaceInfo("pif-1", "", None, None, pif1Id, "pif-1-old")))
                    ))
                ))
            )));

        var result = addFallbackData(hostState).Run();

        var resultHostState = result.Should().BeSuccess().Subject;

        resultHostState.HostAdapters.Adapters.Should().HaveCount(2);

        var pif1Info = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-1").WhoseValue;
        pif1Info.InterfaceId.Should().Be(pif1Id);
        pif1Info.Name.Should().Be("pif-1");
        pif1Info.ConfiguredName.Should().BeSome().Which.Should().Be("pif-1-old");
        pif1Info.IsPhysical.Should().BeTrue();

        var pif1OldInfo = resultHostState.HostAdapters.Adapters.ToDictionary().Should().ContainKey("pif-1-old").WhoseValue;
        pif1OldInfo.InterfaceId.Should().Be(pif1Id);
        pif1OldInfo.Name.Should().Be("pif-1");
        pif1OldInfo.ConfiguredName.Should().BeSome().Which.Should().Be("pif-1-old");
        pif1OldInfo.IsPhysical.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateChanges_DefaultConfigWithDefaultHostState_GeneratesExpectedChanges()
    {
        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => generateChanges(hostState, c, false))
            .Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.CreateOverlaySwitch),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StartOVN),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddBridge),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_DefaultConfigWithDefaultRouteOnHost_GeneratesExpectedChanges()
    {
        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq1(new HostRouteInfo(None, IPNetwork2.Parse("0.0.0.0/0"), IPAddress.Parse("192.168.0.1"))),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => generateChanges(hostState, c, false))
            .Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.CreateOverlaySwitch),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StartOVN),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddBridge),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_MultipleOverlaySwitches_GeneratesRebuildOfOverlaySwitch()
    {
        _hostNetworkCommandsMock.Setup(x => x.GetVmAdaptersBySwitch(It.IsAny<Guid>()))
            .Returns(SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq(new VMSwitch { Id = Guid.NewGuid(), Name = EryphConstants.OverlaySwitchName },
                new VMSwitch { Id = Guid.NewGuid(), Name = EryphConstants.OverlaySwitchName }),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => generateChanges(hostState, c, false))
            .Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StopOVN),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.RebuildOverLaySwitch),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StartOVN),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddBridge),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_EryphNetNatWithIncorrectIpRange_GeneratesRebuildOfNetNat()
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-test",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.249.248.1",
                            Network = "10.249.248.0/24",
                        }
                    ]
                },
            ],
        };

        var hostState = CreateStateWithOverlaySwitch() with
        {
            NetNat = Seq1(new NetNat
            {
                Name = "eryph_default_default",
                InternalIPInterfaceAddressPrefix = "10.249.248.0/22",
            }),
        };

        var result = await generateChanges(hostState, providersConfig,false).Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddBridge),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.RemoveNetNat),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.ConfigureNatIp),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.AddNetNat),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_RenamedPhysicalAdapter_GeneratesRebuildOfBridgeWithNewAdapterName()
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-test",
                    Adapters = ["test-adapter"],
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.249.248.1",
                            Network = "10.249.248.0/24",
                        }
                    ]
                },
            ],
        };

        var interfaceId = Guid.NewGuid();

        var hostState = CreateStateWithOverlaySwitch() with
        {
            VMSwitches = Seq1(new VMSwitch
            {
                Id = OverlaySwitchId,
                Name = EryphConstants.OverlaySwitchName,
                NetAdapterInterfaceGuid = [interfaceId],
            }),
            OvsBridges = new OvsBridgesInfo(HashMap(
                ("br-test", new OvsBridgeInfo("br-test", HashMap(
                    ("br-test", new OvsBridgePortInfo(
                        "br-test", "br-test", None, None, None, Seq<OvsInterfaceInfo>())),
                    ("test-adapter", new OvsBridgePortInfo(
                        "test-adapter", "br-test", None, None, None, Seq1(
                            new OvsInterfaceInfo("test-adapter", "", None, None, interfaceId, "test-adapter")))))
                )))),
            HostAdapters = new HostAdaptersInfo(HashMap(
                ("renamed-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, None, true, OverlaySwitchId)),
                ("br-test", new HostAdapterInfo("br-test", Guid.NewGuid(), None, false, OverlaySwitchId))
                )),
        };

        var result = await generateChanges(hostState, providersConfig, true).Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation =>
            {
                operation.Operation.Should().Be(NetworkChangeOperation.RemoveAdapterPort);
                operation.Args.Should().Equal("test-adapter", "br-test");
                operation.Force.Should().BeTrue();
            },
            operation =>
            {
                operation.Operation.Should().Be(NetworkChangeOperation.AddAdapterPort);
                operation.Args.Should().Equal("renamed-adapter", "br-test");
                operation.Force.Should().BeTrue();
            },
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_RenamedPhysicalAdapterIsAlreadyUsed_GeneratesNoAdapterChanges()
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-test",
                    Adapters = ["test-adapter"],
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.249.248.1",
                            Network = "10.249.248.0/24",
                        }
                    ]
                },
            ],
        };

        var interfaceId = Guid.NewGuid();

        var hostState = CreateStateWithOverlaySwitch() with
        {
            VMSwitches = Seq1(new VMSwitch
            {
                Id = OverlaySwitchId,
                Name = EryphConstants.OverlaySwitchName,
                NetAdapterInterfaceGuid = [interfaceId],
            }),
            OvsBridges = new OvsBridgesInfo(HashMap(
                ("br-test", new OvsBridgeInfo("br-test", HashMap(
                    ("br-test", new OvsBridgePortInfo(
                        "br-test", "br-test", None, None, None, Seq<OvsInterfaceInfo>())),
                    ("renamed-adapter", new OvsBridgePortInfo(
                        "renamed-adapter", "br-test", None, None, None, Seq1(
                            new OvsInterfaceInfo("renamed-adapter", "", None, None, interfaceId, "test-adapter")))))
                )))),
            HostAdapters = new HostAdaptersInfo(HashMap(
                ("renamed-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, None, true, OverlaySwitchId)),
                ("br-test", new HostAdapterInfo("br-test", Guid.NewGuid(), None, false, OverlaySwitchId))
                )),
        };

        var result = await generateChanges(hostState, providersConfig, true).Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_AddOverlayWithBond_GeneratesChanges()
    {
        _hostNetworkCommandsMock.Setup(x => x.GetVmAdaptersBySwitch(It.IsAny<Guid>()))
            .Returns(SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));

        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-pif",
                    Adapters = ["pif-1", "pif-2"],
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.249.248.1",
                            Network = "10.249.248.0/24",
                        }
                    ]
                },
            ],
        };

        var pif1Id = Guid.NewGuid();
        var pif2Id = Guid.NewGuid();

        var hostState = CreateStateWithOverlaySwitch() with
        {
            HostAdapters = new HostAdaptersInfo(HashMap(
                ("pif-1", new HostAdapterInfo("pif-1", pif1Id, None, true, None)),
                ("pif-2", new HostAdapterInfo("pif-2", pif2Id, None, true, None)))),
        };

        var result = await generateChanges(hostState, providersConfig, true).Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StopOVN),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.RebuildOverLaySwitch),
            operation => operation.Operation.Should().Be(NetworkChangeOperation.StartOVN),
            operation =>
            {
                operation.Operation.Should().Be(NetworkChangeOperation.AddBridge);
                operation.Args.Should().Equal("br-pif");
            },
            operation =>
            {
                operation.Operation.Should().Be(NetworkChangeOperation.AddBondPort);
                operation.Args.Should().Equal("br-pif-bond", "pif-1, pif-2", "br-pif");
            },
            operation => operation.Operation.Should().Be(NetworkChangeOperation.UpdateBridgeMapping));
    }

    [Fact]
    public async Task GenerateChanges_HostAdapterIsMissing_ReturnsError()
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-test",
                    Adapters = ["missing-adapter"],
                },
            ],
        };

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig, false).Run(_runtime);

        result.Should().BeFail().Which.Message
            .Should().Be("The host adapter 'missing-adapter' does not exist.");
    }

    [Fact]
    public async Task GenerateChanges_HostAdapterIsAttachedToOtherSwitch_ReturnsError()
    {
        var adapterId = Guid.NewGuid();
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.Overlay,
                    BridgeName = "br-test",
                    Adapters = ["test-adapter"],
                },
            ],
        };

        var otherSwitchId = Guid.NewGuid();
        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq1(new VMSwitch
            {
                Id = otherSwitchId,
                Name = "other-switch",
                NetAdapterInterfaceGuid = [adapterId],
            }),
            new HostAdaptersInfo(HashMap(
                ("test-adapter", new HostAdapterInfo("test-adapter", adapterId, None, true, otherSwitchId)))),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig, false).Run(_runtime);

        var error = result.Should().BeFail().Subject;
        error.Message.Should().Be("Some host adapters are used by other Hyper-V switches.");
        error.Inner.Should().BeSome()
            .Which.Message.Should().Be("The host adapter 'test-adapter' is used by the Hyper-V switch 'other-switch'.");
    }

    [Theory]
    [InlineData("10.0.0.0/22", "10.0.1.0/24")]
    [InlineData("10.0.1.0/24", "10.0.0.0/22")]
    public async Task GenerateChanges_EryphNatOverlapsOtherNat_ReturnsError(
        string eryphNetwork, string otherNetwork)
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-test",
                    Subnets = 
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.0.1.1",
                            Network = eryphNetwork,
                        }
                    ]
                },
            ],
        };

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq1(new VMSwitch { Name = EryphConstants.OverlaySwitchName }),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq1(new NetNat { Name = "other-nat", InternalIPInterfaceAddressPrefix = otherNetwork }),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig, false).Run(_runtime);

        result.Should().BeFail().Which.Message
            .Should().Be($"The IP range '{eryphNetwork}' of the provider 'test-provider' overlaps the IP range '{otherNetwork}' of the NAT 'other-nat' which is not managed by eryph.");
    }

    [Theory]
    [InlineData("10.0.0.0/22", "10.0.1.0/24")]
    [InlineData("10.0.1.0/24", "10.0.0.0/22")]
    public async Task GenerateChanges_EryphNatOverlapsUnmanagedHostRoute_ReturnsError(
        string eryphNetwork, string otherNetwork)
    {
        var providersConfig = new NetworkProvidersConfiguration
        {
            NetworkProviders =
            [
                new NetworkProvider
                {
                    Name = "test-provider",
                    Type = NetworkProviderType.NatOverlay,
                    BridgeName = "br-test",
                    Subnets =
                    [
                        new NetworkProviderSubnet
                        {
                            Name = "default",
                            Gateway = "10.0.1.1",
                            Network = eryphNetwork,
                        }
                    ]
                },
            ],
        };

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq1(new VMSwitch
            {
                Id = OverlaySwitchId,
                Name = EryphConstants.OverlaySwitchName
            }),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq1(new HostRouteInfo(None, IPNetwork2.Parse(otherNetwork), IPAddress.Parse("0.0.0.0"))),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig, false).Run(_runtime);

        result.Should().BeFail().Which.Message
            .Should().Be($"The IP range '{eryphNetwork}' of the provider 'test-provider' overlaps the IP range '{otherNetwork}' of a network route which is not managed by eryph.");
    }

    private static HostState CreateStateWithOverlaySwitch() =>
        new HostState(
            Seq1(new VMSwitchExtension
            {
                Id = Guid.NewGuid().ToString(),
                Enabled = true,
                SwitchId = OverlaySwitchId,
                SwitchName = EryphConstants.OverlaySwitchName,
            }),
            Seq1(new VMSwitch
            {
                Id = OverlaySwitchId,
                Name = EryphConstants.OverlaySwitchName,
            }),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            Seq<HostRouteInfo>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));
}
