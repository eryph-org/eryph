using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using Moq;

using static Eryph.Modules.VmHostAgent.Networks.ProviderNetworkUpdate<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Networks;

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
    public async Task GenerateChanges_DefaultConfigWithDefaultHostState_GeneratesExpectedChanges()
    {
        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq<VMSwitch>(),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => generateChanges(hostState, c))
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
        _hostNetworkCommandsMock.Setup(x => x.GetNetAdaptersBySwitch(It.IsAny<Guid>()))
            .Returns(SuccessAff(Seq<TypedPsObject<VMNetworkAdapter>>()));

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq(new VMSwitch { Id = Guid.NewGuid(), Name = EryphConstants.OverlaySwitchName },
                new VMSwitch { Id = Guid.NewGuid(), Name = EryphConstants.OverlaySwitchName }),
            new HostAdaptersInfo(HashMap<string, HostAdapterInfo>()),
            Seq<NetNat>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await importConfig(NetworkProvidersConfiguration.DefaultConfig)
            .Bind(c => generateChanges(hostState, c))
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

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

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
                    Adapters = ["renamed-adapter"],
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
                    ("br-test", new OvsBridgePortInfo("br-test", "br-test", None, None, None,
                        Seq<OvsBridgeInterfaceInfo>())),
                    ("test-adapter", new OvsBridgePortInfo("br-test", "test-adapter", None, None, None,
                        Seq1(new OvsBridgeInterfaceInfo("test-adapter", "", interfaceId, "test-adapter")))))
                )))),
            HostAdapters = new HostAdaptersInfo(HashMap(
                ("renamed-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, "test-adapter", true)),
                ("test-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, "test-adapter", true)),
                ("br-test", new HostAdapterInfo("br-test", Guid.NewGuid(), None, false))
                )),
        };

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

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
                    Adapters = ["renamed-adapter"],
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
                    ("br-test", new OvsBridgePortInfo("br-test", "br-test", None, None, None,
                        Seq<OvsBridgeInterfaceInfo>())),
                    ("renamed-adapter", new OvsBridgePortInfo("br-test", "renamed-adapter", None, None, None,
                        Seq1(new OvsBridgeInterfaceInfo("renamed-adapter", "", interfaceId, "test-adapter")))))
                )))),
            HostAdapters = new HostAdaptersInfo(HashMap(
                ("renamed-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, "test-adapter", true)),
                ("test-adapter", new HostAdapterInfo("renamed-adapter", interfaceId, "test-adapter", true)),
                ("br-test", new HostAdapterInfo("br-test", Guid.NewGuid(), None, false))
                )),
        };

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

        result.Should().BeSuccess().Which.Operations.Should().SatisfyRespectively(
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
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

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

        var hostState = new HostState(
            Seq<VMSwitchExtension>(),
            Seq1(new VMSwitch
            {
                Name = "other-switch",
                NetAdapterInterfaceGuid = [adapterId],
            }),
            new HostAdaptersInfo(HashMap(
                ("test-adapter", new HostAdapterInfo("test-adapter", adapterId, None, true)))),
            Seq<NetNat>(),
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

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
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));

        var result = await generateChanges(hostState, providersConfig).Run(_runtime);

        result.Should().BeFail().Which.Message
            .Should().Be($"The IP range '{eryphNetwork}' of the provider 'test-provider' overlaps the IP range '{otherNetwork}' of the NAT 'other-nat' which is not managed by eryph.");
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
            new OvsBridgesInfo(HashMap<string, OvsBridgeInfo>()));
}
