using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using Moq;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.HyperV.Test;

public class ConvergeNetworkAdaptersTests
{
    private readonly ConvergeFixture _fixture;
    private readonly TypedPsObject<VirtualMachineInfo> _vmInfo;
    private readonly TypedPsObject<VMNetworkAdapter> _existingAdapter;
    private readonly Mock<IHyperVOvsPortManager> _portManagerMock = new();

    public ConvergeNetworkAdaptersTests()
    {
        _fixture = new()
        {
            PortManager = _portManagerMock.Object,
            HostInfo = new()
            {
                NetworkProviderConfiguration = new()
                {
                    NetworkProviders = 
                    [
                        new NetworkProvider()
                        {
                            Name = "default",
                            Type = NetworkProviderType.NatOverlay,
                        },
                        new NetworkProvider()
                        {
                            Name = "flat",
                            Type = NetworkProviderType.Flat,
                            SwitchName = "test-switch",
                        },
                    ],
                },
            },
        };
        _vmInfo = _fixture.Engine.ToPsObject(new VirtualMachineInfo());
        _existingAdapter = _fixture.Engine.ToPsObject(new VMNetworkAdapter()
        {
            Id = "eth0Id",
            Name = "eth0",
            MacAddress = "00:02:04:06:08:10",
            SwitchName = EryphConstants.OverlaySwitchName,
            Connected = true,
            DhcpGuard = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            MacAddressSpoofing = OnOffState.Off,
        });
        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Get-VMNetworkAdapter")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                return Seq1(_fixture.Engine.ConvertPsObject(_existingAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            return Error.New($"Unexpected command: {command}.");
        };
        _portManagerMock.Setup(m => m.GetPortName("eth0Id"))
            .Returns(RightAsync<Error, Option<string>>(Some("port0")));
    }

    [Fact]
    public async Task Converge_AdapterConfiguredAndMissing_AddsAdapter()
    {
        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
            new CatletNetworkConfig
            {
                AdapterName = "eth1",
            },
        ];
        _fixture.NetworkSettings =
        [
            new MachineNetworkSettings
            {
                AdapterName = "eth0",
                MacAddress = "00:02:04:06:08:10",
                PortName = "port0",
                NetworkProviderName = "default",
            },
            new MachineNetworkSettings
            {
                AdapterName = "eth1",
                MacAddress = "00:12:14:16:18:20",
                PortName = "port1",
                NetworkProviderName = "flat",
                MacAddressSpoofing = true,
                DhcpGuard = true,
                RouterGuard = true,
            },
        ];

        var adapterAdded = false;
        var adapterSettingsUpdated = false;

        var newAdapter = _fixture.Engine.ToPsObject<object>(new VMNetworkAdapter
        {
            Id = "eth1Id",
            MacAddressSpoofing = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            DhcpGuard = OnOffState.Off,
        });

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Get-VMNetworkAdapter")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeComplete();

                return Seq1(_fixture.Engine.ConvertPsObject(_existingAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            if (command.ToString().StartsWith("Add-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Add-VMNetworkAdapter")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeParam("Name", "eth1")
                    .ShouldBeParam("StaticMacAddress", "00:12:14:16:18:20")
                    .ShouldBeParam("SwitchName", "test-switch")
                    .ShouldBeFlag("Passthru")
                    .ShouldBeComplete();
                adapterAdded = true;
                return Seq1(newAdapter);
            }

            return Error.New($"Unexpected command: {command}.");
        };

        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().StartsWith("Set-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Set-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", newAdapter.PsObject)
                    .ShouldBeParam("MacAddressSpoofing", OnOffState.On)
                    .ShouldBeParam("DhcpGuard", OnOffState.On)
                    .ShouldBeParam("RouterGuard", OnOffState.On)
                    .ShouldBeComplete();
                adapterSettingsUpdated = true;
                return unit;
            }

            return Error.New($"Unexpected command {command}");
        };

        _portManagerMock.Setup(m => m.GetPortName("eth1Id"))
            .Returns(RightAsync<Error, Option<string>>(Some("")))
            .Verifiable();

        _portManagerMock.Setup(m => m.SetPortName("eth1Id", "port1"))
            .Returns(RightAsync<Error, Unit>(unit))
            .Verifiable();

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);
        
        result.Should().BeRight();
        adapterAdded.Should().BeTrue();
        adapterSettingsUpdated.Should().BeTrue();
        _portManagerMock.Verify();
    }

    [Fact]
    public async Task Converge_AdapterConfigChanged_UpdatesAdapter()
    {
        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
        ];
        _fixture.NetworkSettings =
        [
            new MachineNetworkSettings
            {
                AdapterName = "eth0",
                MacAddress = "00:12:14:16:18:20",
                PortName = "port1",
                NetworkProviderName = "flat",
                MacAddressSpoofing = true,
                DhcpGuard = true,
                RouterGuard = true,
            },
        ];

        var adapterMacAddressUpdated = false;
        var adapterSettingsUpdated = false;
        var adapterConnected = false;
        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().Contains("StaticMacAddress"))
            {
                command.ShouldBeCommand("Set-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("StaticMacAddress", "00:12:14:16:18:20")
                    .ShouldBeComplete();
                adapterMacAddressUpdated = true;
                return unit;
            }

            if (command.ToString().Contains("MacAddressSpoofing"))
            {
                command.ShouldBeCommand("Set-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("MacAddressSpoofing", OnOffState.On)
                    .ShouldBeParam("DhcpGuard", OnOffState.On)
                    .ShouldBeParam("RouterGuard", OnOffState.On)
                    .ShouldBeComplete();
                adapterSettingsUpdated = true;
                return unit;
            }

            if (command.ToString().StartsWith("Connect-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Connect-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("SwitchName", "test-switch")
                    .ShouldBeComplete();
                adapterConnected = true;
                return unit;
            }

            return Error.New ($"Unexpected command {command}");
        };

        _portManagerMock.Setup(m => m.SetPortName("eth0Id", "port1"))
            .Returns(RightAsync<Error, Unit>(unit))
            .Verifiable();

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeRight();
        adapterMacAddressUpdated.Should().BeTrue();
        adapterSettingsUpdated.Should().BeTrue();
        adapterConnected.Should().BeTrue();
        _portManagerMock.Verify();
    }

    [Fact]
    public async Task Converge_AdapterWithoutNetwork_AddsDisconnectedAdapter()
    {
        var eth1MacAddress = EryphMacAddress.New("00:12:14:16:18:20").Value;

        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
        ];
        _fixture.Config.NetworkAdapters =
        [
            new CatletNetworkAdapterConfig { Name = "eth0" },
            new CatletNetworkAdapterConfig { Name = "eth1", MacAddress = "00:12:14:16:18:20" },
        ];
        _fixture.NetworkSettings =
        [
            new MachineNetworkSettings
            {
                AdapterName = "eth0",
                MacAddress = "00:02:04:06:08:10",
                PortName = "port0",
                NetworkProviderName = "default",
            },
        ];

        var adapterAdded = false;

        var newAdapter = _fixture.Engine.ToPsObject<object>(new VMNetworkAdapter
        {
            Id = "eth1Id",
            Name = "eth1",
            MacAddressSpoofing = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            DhcpGuard = OnOffState.Off,
        });

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_existingAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            if (command.ToString().StartsWith("Add-VMNetworkAdapter"))
            {
                // A disconnected adapter is added without a virtual switch.
                command.ShouldBeCommand("Add-VMNetworkAdapter")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeParam("Name", "eth1")
                    .ShouldBeParam("StaticMacAddress", eth1MacAddress)
                    .ShouldBeFlag("Passthru")
                    .ShouldBeComplete();
                adapterAdded = true;
                return Seq1(newAdapter);
            }

            return Error.New($"Unexpected command: {command}.");
        };

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeRight();
        adapterAdded.Should().BeTrue();
        // The existing connected adapter eth0 already has the correct port, so its
        // OVS port name must not be rewritten.
        _portManagerMock.Verify(m => m.SetPortName("eth0Id", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Converge_FlatNetworkAndAdapterWithoutNetwork_ConvergesBoth()
    {
        var eth1MacAddress = EryphMacAddress.New("00:22:24:26:28:30").Value;

        // eth0 is attached to a flat network, eth1 is not attached to any network.
        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
        ];
        _fixture.Config.NetworkAdapters =
        [
            new CatletNetworkAdapterConfig { Name = "eth0" },
            new CatletNetworkAdapterConfig { Name = "eth1", MacAddress = "00:22:24:26:28:30" },
        ];
        _fixture.NetworkSettings =
        [
            new MachineNetworkSettings
            {
                AdapterName = "eth0",
                MacAddress = "00:12:14:16:18:20",
                PortName = "port1",
                NetworkProviderName = "flat",
                MacAddressSpoofing = true,
                DhcpGuard = true,
                RouterGuard = true,
            },
        ];

        var disconnectedAdapterAdded = false;
        var flatAdapterConnected = false;

        var newAdapter = _fixture.Engine.ToPsObject<object>(new VMNetworkAdapter
        {
            Id = "eth1Id",
            Name = "eth1",
            MacAddressSpoofing = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            DhcpGuard = OnOffState.Off,
        });

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_existingAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            if (command.ToString().StartsWith("Add-VMNetworkAdapter"))
            {
                // The disconnected adapter is added without a virtual switch.
                command.ShouldBeCommand("Add-VMNetworkAdapter")
                    .ShouldBeParam("VM", _vmInfo.PsObject)
                    .ShouldBeParam("Name", "eth1")
                    .ShouldBeParam("StaticMacAddress", eth1MacAddress)
                    .ShouldBeFlag("Passthru")
                    .ShouldBeComplete();
                disconnectedAdapterAdded = true;
                return Seq1(newAdapter);
            }

            return Error.New($"Unexpected command: {command}.");
        };

        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().Contains("StaticMacAddress"))
            {
                command.ShouldBeCommand("Set-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("StaticMacAddress", "00:12:14:16:18:20")
                    .ShouldBeComplete();
                return unit;
            }

            if (command.ToString().Contains("MacAddressSpoofing"))
            {
                command.ShouldBeCommand("Set-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("MacAddressSpoofing", OnOffState.On)
                    .ShouldBeParam("DhcpGuard", OnOffState.On)
                    .ShouldBeParam("RouterGuard", OnOffState.On)
                    .ShouldBeComplete();
                return unit;
            }

            if (command.ToString().StartsWith("Connect-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Connect-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                    .ShouldBeParam("SwitchName", "test-switch")
                    .ShouldBeComplete();
                flatAdapterConnected = true;
                return unit;
            }

            return Error.New($"Unexpected command {command}");
        };

        _portManagerMock.Setup(m => m.SetPortName("eth0Id", "port1"))
            .Returns(RightAsync<Error, Unit>(unit))
            .Verifiable();

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeRight();
        disconnectedAdapterAdded.Should().BeTrue();
        flatAdapterConnected.Should().BeTrue();
        _portManagerMock.Verify();
    }

    [Fact]
    public async Task Converge_NetworkRemovedFromAdapter_DisconnectsAdapter()
    {
        var macAddress = EryphMacAddress.New("00:02:04:06:08:10").Value;
        var connectedAdapter = _fixture.Engine.ToPsObject(new VMNetworkAdapter
        {
            Id = "eth0Id",
            Name = "eth0",
            MacAddress = macAddress,
            SwitchName = EryphConstants.OverlaySwitchName,
            Connected = true,
            DhcpGuard = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            MacAddressSpoofing = OnOffState.Off,
        });

        // The adapter is still configured but is no longer attached to any network.
        _fixture.Config.Networks = [];
        _fixture.Config.NetworkAdapters =
        [
            new CatletNetworkAdapterConfig { Name = "eth0", MacAddress = "00:02:04:06:08:10" },
        ];
        _fixture.NetworkSettings = [];

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(connectedAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            return Error.New($"Unexpected command: {command}.");
        };

        var adapterDisconnected = false;
        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().StartsWith("Disconnect-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Disconnect-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", connectedAdapter.PsObject)
                    .ShouldBeComplete();
                adapterDisconnected = true;
                return unit;
            }

            return Error.New($"Unexpected command {command}");
        };

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeRight();
        adapterDisconnected.Should().BeTrue();
        // A disconnected adapter has no OVS port, so the port manager must not be touched.
        _portManagerMock.Verify(m => m.GetPortName(It.IsAny<string>()), Times.Never);
        _portManagerMock.Verify(m => m.SetPortName(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Converge_NetworkAddedToDisconnectedAdapter_ConnectsAdapter()
    {
        var macAddress = EryphMacAddress.New("00:02:04:06:08:10").Value;

        // The adapter currently exists in Hyper-V but is not connected to any switch.
        var disconnectedAdapter = _fixture.Engine.ToPsObject(new VMNetworkAdapter
        {
            Id = "eth0Id",
            Name = "eth0",
            MacAddress = macAddress,
            SwitchName = "",
            Connected = false,
            DhcpGuard = OnOffState.Off,
            RouterGuard = OnOffState.Off,
            MacAddressSpoofing = OnOffState.Off,
        });

        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
        ];
        _fixture.Config.NetworkAdapters =
        [
            new CatletNetworkAdapterConfig { Name = "eth0" },
        ];
        _fixture.NetworkSettings =
        [
            new MachineNetworkSettings
            {
                AdapterName = "eth0",
                // The MAC address is preserved when the adapter is attached to a network.
                MacAddress = macAddress,
                PortName = "port0",
                NetworkProviderName = "default",
            },
        ];

        _fixture.Engine.GetObjectCallback = (_, command) =>
        {
            if (command.ToString().StartsWith("Get-VMNetworkAdapter"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(disconnectedAdapter));
            }

            if (command.ToString().StartsWith("Get-VM"))
            {
                return Seq1(_fixture.Engine.ConvertPsObject(_vmInfo));
            }

            return Error.New($"Unexpected command: {command}.");
        };

        var adapterConnected = false;
        _fixture.Engine.RunCallback = command =>
        {
            if (command.ToString().StartsWith("Connect-VMNetworkAdapter"))
            {
                command.ShouldBeCommand("Connect-VMNetworkAdapter")
                    .ShouldBeParam("VMNetworkAdapter", disconnectedAdapter.PsObject)
                    .ShouldBeParam("SwitchName", EryphConstants.OverlaySwitchName)
                    .ShouldBeComplete();
                adapterConnected = true;
                return unit;
            }

            return Error.New($"Unexpected command {command}");
        };

        // The adapter has no OVS port yet.
        _portManagerMock.Setup(m => m.GetPortName("eth0Id"))
            .Returns(RightAsync<Error, Option<string>>(Some("")));
        _portManagerMock.Setup(m => m.SetPortName("eth0Id", "port0"))
            .Returns(RightAsync<Error, Unit>(unit))
            .Verifiable();

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeRight();
        adapterConnected.Should().BeTrue();
        _portManagerMock.Verify();
    }

    [Fact]
    public async Task Converge_AdapterAttachedToNetworkButSettingsMissing_Fails()
    {
        // eth0 is attached to a network but no network settings were generated for it.
        // This must fail rather than silently converging eth0 as a disconnected adapter.
        _fixture.Config.Networks =
        [
            new CatletNetworkConfig
            {
                AdapterName = "eth0",
            },
        ];
        _fixture.Config.NetworkAdapters =
        [
            new CatletNetworkAdapterConfig { Name = "eth0" },
        ];
        _fixture.NetworkSettings = [];

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);

        result.Should().BeLeft().Which.Message.Should().Contain("eth0");
    }

    [Fact]
    public async Task Converge_AdapterNotConfigured_RemovesAdapter()
    {
        bool adapterRemoved = false;

        _fixture.Engine.RunCallback = command =>
        {
            command.ShouldBeCommand("Remove-VMNetworkAdapter")
                .ShouldBeParam("VMNetworkAdapter", _existingAdapter.PsObject)
                .ShouldBeComplete();
            adapterRemoved = true;
            return unit;
        };

        var convergeTask = new ConvergeNetworkAdapters(_fixture.Context);
        var result = await convergeTask.Converge(_vmInfo);
        result.Should().BeRight();
        adapterRemoved.Should().BeTrue();
    }
}
