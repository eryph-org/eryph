using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Networking;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using Xunit;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Test.Networking;

public class NetworkNeighborsUpdateTests
{
    private readonly TestPowershellEngine _powershellEngine = new(new FakeTypeMapping());

    [Fact]
    public async Task RemoveOutdatedNetworkNeighbors_ValidUpdatedNeighbors_RemovesOutdatedNeighbors()
    {
        Seq<CimNetworkNeighbor> existingNeighbors = Seq(
            new CimNetworkNeighbor()
            {
                IpAddress = "192.168.1.1",
                LinkLayerAddress = "02:11:22:33:44:55",
            },
            new CimNetworkNeighbor()
            {
                IpAddress = "192.168.1.2",
                LinkLayerAddress = "02:11:22:33:44:56",
            },
            new CimNetworkNeighbor()
            {
                IpAddress = "192.168.1.3",
                LinkLayerAddress = "02:11:22:33:44:57",
            },
            new CimNetworkNeighbor()
            {
                IpAddress = "192.168.1.4",
                LinkLayerAddress = "02:11:22:33:44:58",
            });

        AssertCommand? getCommand = null;
        AssertCommand? removeCommand = null;
        _powershellEngine.GetObjectCallback = (_, command) =>
        {
            getCommand = command;
            return existingNeighbors
                .Cast<object>()
                .Map(n => _powershellEngine.ToPsObject(n))
                .ToSeq();
        };

        _powershellEngine.RunCallback = command =>
        {
            removeCommand = command;
            return unit;
        };

        var result = await NetworkNeighborsUpdate.RemoveOutdatedNetworkNeighbors(
            _powershellEngine,
            Seq(("192.168.1.1", "02:11:22:33:44:55"),
                ("192.168.1.2", "02:99:99:99:99:99"),
                ("192.168.1.3", "")
            ));

        result.Should().BeRight();

        getCommand.Should().NotBeNull();
        getCommand!.ShouldBeCommand("Get-NetNeighbor")
            .ShouldBeParam<string[]>(
                "IPAddress",
                param => param.Should().Equal("192.168.1.1", "192.168.1.2", "192.168.1.3"))
            .ShouldBeParam("ErrorAction", "SilentlyContinue");

        removeCommand.Should().NotBeNull();
        removeCommand!.ShouldBeCommand("Remove-NetNeighbor")
            .ShouldBeParam<PSObject[]>(
                "InputObject",
                param => param.Should().SatisfyRespectively(
                    o =>
                    {
                        var n = o.BaseObject.Should().BeOfType<CimNetworkNeighbor>().Subject;
                        n.IpAddress.Should().Be("192.168.1.2");
                    },
                    o =>
                    {
                        var n = o.BaseObject.Should().BeOfType<CimNetworkNeighbor>().Subject;
                        n.IpAddress.Should().Be("192.168.1.3");
                    }));
    }

    [Theory]
    [InlineData("999.999.999.999", "02:11:22:33:44:55")]
    [InlineData("192.168.1.1", "invalid")]
    public async Task RemoveOutdatedNetworkNeighbors_InvalidUpdatedNeighbor_ReturnsError(
        string ipAddress, string macAddress)
    {
        var result = await NetworkNeighborsUpdate.RemoveOutdatedNetworkNeighbors(
            _powershellEngine,
            Seq1((ipAddress, macAddress)));

        result.Should().BeLeft().Which.Message.Should().Contain("invalid");
    }


    [Theory]
    [InlineData("999.999.999.999", "02:11:22:33:44:55")]
    [InlineData("192.168.1.1", "invalid")]
    public async Task RemoveOutdatedNetworkNeighbors_InvalidExistingNeighbor_ReturnsError(
        string ipAddress, string macAddress)
    {
        _powershellEngine.GetObjectCallback = (_, _) =>
        {
            return Seq1(new CimNetworkNeighbor()
                {
                    IpAddress = ipAddress,
                    LinkLayerAddress = macAddress,
                })
                .Cast<object>()
                .Map(n => _powershellEngine.ToPsObject(n))
                .ToSeq();
        };

        var result = await NetworkNeighborsUpdate.RemoveOutdatedNetworkNeighbors(
            _powershellEngine, Empty);

        result.Should().BeLeft().Which.Message.Should().Contain("invalid");
    }
}
