using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
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

namespace Eryph.VmManagement.Test;

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
