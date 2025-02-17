using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.OVN.Model;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Moq;

using static Eryph.Modules.VmHostAgent.Networks.HostStateProvider<Eryph.Modules.VmHostAgent.Test.TestRuntime>;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Test.Networks;

public class HostStateProviderTests
{
    private readonly Mock<IOVSControl> _ovsControlMock = new();
    private readonly Mock<INetworkProviderManager> _networkProviderManagerMock = new();
    private readonly Mock<IHostNetworkCommands<TestRuntime>> _hostNetworkCommandsMock = new();
    private readonly Mock<ISyncClient> _syncClientMock = new();
    private readonly TestRuntime _runtime;

    public HostStateProviderTests()
    {
        _runtime = TestRuntime.New(
            _ovsControlMock.Object,
            _syncClientMock.Object,
            _hostNetworkCommandsMock.Object,
            _networkProviderManagerMock.Object);
    }

    [Fact]
    public async Task GetHostState_ComplexHostState_ReturnsExpectedData()
    {
        var switchId = Guid.NewGuid();
        var switchExtensionId = Guid.NewGuid().ToString();
        var hostAdapterId = Guid.NewGuid();

        _hostNetworkCommandsMock.Setup(x => x.GetSwitchExtensions())
            .Returns(SuccessAff(Seq1(new VMSwitchExtension
            {
                Enabled = true,
                Id = switchExtensionId, 
                SwitchId = switchId,
                SwitchName = EryphConstants.OverlaySwitchName
            })));

        _hostNetworkCommandsMock.Setup(x => x.GetSwitches())
            .Returns(SuccessAff(Seq1(new VMSwitch
            {
                Id = switchId,
                Name = EryphConstants.OverlaySwitchName,
                NetAdapterInterfaceGuid = [hostAdapterId],
            })));

        _hostNetworkCommandsMock.Setup(x => x.GetPhysicalAdapters())
            .Returns(SuccessAff(Seq1(new HostNetworkAdapter
            {
                InterfaceGuid = hostAdapterId,
                Name = "test-adapter",
            })));

        _hostNetworkCommandsMock.Setup(x => x.GetAdapterNames())
            .Returns(SuccessAff(Seq("test-adapter", "other-adapter")));

        _hostNetworkCommandsMock.Setup(x => x.GetNetNat())
            .Returns(SuccessAff(Seq1(new NetNat
            {
                Name = "test-nat",
                InternalIPInterfaceAddressPrefix = "10.0.0.0/24",
            })));

        var br1Id = Guid.NewGuid();
        var br1Port1Id = Guid.NewGuid();
        var br1Port1Interface1Id = Guid.NewGuid();
        var br1Port1Interface2Id = Guid.NewGuid();
        var br1Port2Id = Guid.NewGuid();
        var br1Port2Interface1Id = Guid.NewGuid();

        var br2Id = Guid.NewGuid();
        var br2Port1Id = Guid.NewGuid();
        var br2Port1Interface1Id = Guid.NewGuid();
        var br2Port1Interface2Id = Guid.NewGuid();
        var br2Port2Id = Guid.NewGuid();
        var br2Port2Interface1Id = Guid.NewGuid();

        _ovsControlMock.Setup(x => x.GetBridges(It.IsAny<CancellationToken>()))
            .Returns(Seq(
                OVSEntity.FromValueMap<Bridge>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Id)),
                    ("name", OVSValue<string>.New("br-1")),
                    ("ports", OVSReference.New(Seq(br1Port1Id, br1Port2Id))))),
                OVSEntity.FromValueMap<Bridge>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Id)),
                    ("name", OVSValue<string>.New("br-2")),
                    ("ports", OVSReference.New(Seq(br2Port1Id, br2Port2Id)))))
                ));

        _ovsControlMock.Setup(x => x.GetPorts(It.IsAny<CancellationToken>()))
            .Returns(Seq(
                OVSEntity.FromValueMap<BridgePort>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Port1Id)),
                    ("name", OVSValue<string>.New("br-1-port-1")),
                    ("interfaces", OVSReference.New(Seq(br1Port1Interface1Id, br1Port1Interface2Id))))),
                OVSEntity.FromValueMap<BridgePort>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Port2Id)),
                    ("name", OVSValue<string>.New("br-1-port-2")),
                    ("interfaces", OVSReference.New(Seq1(br1Port2Interface1Id))))),
                OVSEntity.FromValueMap<BridgePort>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Port1Id)),
                    ("name", OVSValue<string>.New("br-2-port-1")),
                    ("interfaces", OVSReference.New(Seq(br2Port1Interface1Id, br2Port1Interface2Id))))),
                OVSEntity.FromValueMap<BridgePort>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Port2Id)),
                    ("name", OVSValue<string>.New("br-2-port-2")),
                    ("interfaces", OVSReference.New(Seq1(br2Port2Interface1Id)))))
                ));

        _ovsControlMock.Setup(x => x.GetInterfaces(It.IsAny<CancellationToken>()))
            .Returns(Seq(
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Port1Interface1Id)),
                    ("name", OVSValue<string>.New("br-1-port-1-if-1")))),
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Port1Interface2Id)),
                    ("name", OVSValue<string>.New("br-1-port-1-if-2")))),
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br1Port2Interface1Id)),
                    ("name", OVSValue<string>.New("br-1-port-2-if-1")))),
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Port1Interface1Id)),
                    ("name", OVSValue<string>.New("br-2-port-1-if-1")))),
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Port1Interface2Id)),
                    ("name", OVSValue<string>.New("br-2-port-1-if-2")))),
                OVSEntity.FromValueMap<Interface>(Map<string, IOVSField>(
                    ("_uuid", OVSValue<Guid>.New(br2Port2Interface1Id)),
                    ("name", OVSValue<string>.New("br-2-port-2-if-1"))))
            ));

        var result = await getHostState().Run(_runtime);

        var hostState = result.Should().BeSuccess().Subject;
        hostState.VMSwitches.Should().SatisfyRespectively(
            vmSwitch =>
            {
                vmSwitch.Id.Should().Be(switchId);
                vmSwitch.Name.Should().Be(EryphConstants.OverlaySwitchName);
                vmSwitch.NetAdapterInterfaceGuid.Should().Equal(hostAdapterId);
            });
        
        hostState.VMSwitchExtensions.Should().SatisfyRespectively(
            vmSwitchExtension =>
            {
                vmSwitchExtension.Id.Should().Be(switchExtensionId);
                vmSwitchExtension.Enabled.Should().BeTrue();
                vmSwitchExtension.SwitchId.Should().Be(switchId);
                vmSwitchExtension.SwitchName.Should().Be(EryphConstants.OverlaySwitchName);
            });

        hostState.NetAdapters.Should().SatisfyRespectively(
            adapter =>
            {
                adapter.InterfaceGuid.Should().Be(hostAdapterId);
                adapter.Name.Should().Be("test-adapter");
            });

        hostState.AllNetAdaptersNames.Should().Equal("test-adapter", "other-adapter");

        var overlaySwítchInfo = hostState.OverlaySwitch.Should().BeSome().Subject;
        overlaySwítchInfo.Id.Should().Be(switchId);
        overlaySwítchInfo.AdaptersInSwitch.Should().Equal("test-adapter");

        var brigde1Info = hostState.OvsBridges.Bridges.ToDictionary().Should().ContainKey("br-1").WhoseValue;
        brigde1Info.Name.Should().Be("br-1");
        
        var br1Port1Info = brigde1Info.Ports.ToDictionary().Should().ContainKey("br-1-port-1").WhoseValue;
        br1Port1Info.PortName.Should().Be("br-1-port-1");
        br1Port1Info.BridgeName.Should().Be("br-1");
        br1Port1Info.Interfaces.Should().SatisfyRespectively(
            i => i.Name.Should().Be("br-1-port-1-if-1"),
            i => i.Name.Should().Be("br-1-port-1-if-2"));
        
        var br1Port2Info = brigde1Info.Ports.ToDictionary().Should().ContainKey("br-1-port-2").WhoseValue;
        br1Port2Info.PortName.Should().Be("br-1-port-2");
        br1Port2Info.BridgeName.Should().Be("br-1");
        br1Port2Info.Interfaces.Should().SatisfyRespectively(
            i => i.Name.Should().Be("br-1-port-2-if-1"));

        var brigde2Info = hostState.OvsBridges.Bridges.ToDictionary().Should().ContainKey("br-2").WhoseValue;
        brigde2Info.Name.Should().Be("br-2");

        var br2Port1Info = brigde2Info.Ports.ToDictionary().Should().ContainKey("br-2-port-1").WhoseValue;
        br2Port1Info.PortName.Should().Be("br-2-port-1");
        br2Port1Info.BridgeName.Should().Be("br-2");
        br2Port1Info.Interfaces.Should().SatisfyRespectively(
            i => i.Name.Should().Be("br-2-port-1-if-1"),
            i => i.Name.Should().Be("br-2-port-1-if-2"));

        var br2Port2Info = brigde2Info.Ports.ToDictionary().Should().ContainKey("br-2-port-2").WhoseValue;
        br2Port2Info.PortName.Should().Be("br-2-port-2");
        br2Port2Info.BridgeName.Should().Be("br-2");
        br2Port2Info.Interfaces.Should().SatisfyRespectively(
            i => i.Name.Should().Be("br-2-port-2-if-1"));
    }
}