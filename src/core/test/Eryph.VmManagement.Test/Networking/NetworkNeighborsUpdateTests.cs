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
    public async Task RemoveOutdatedNetNeighbors_RemovesExpectedNeighbors()
    {
        Seq<CimNetNeighbor> existingNeighbors = Seq(
            new CimNetNeighbor()
            {
                IpAddress = "192.168.1.1",
                LinkLayerAddress = "02:11:22:33:44:55",
            },
            new CimNetNeighbor()
            {
                IpAddress = "192.168.1.2",
                LinkLayerAddress = "02:11:22:33:44:56",
            },
            new CimNetNeighbor()
            {
                IpAddress = "192.168.1.3",
                LinkLayerAddress = "02:11:22:33:44:57",
            },
            new CimNetNeighbor()
            {
                IpAddress = "192.168.1.4",
                LinkLayerAddress = "02:11:22:33:44:58",
            });

        AssertCommand? getCommand = null;
        AssertCommand? removeCommand = null;
        _powershellEngine.GetObjectCallback = (type, command) =>
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

        var result = await NetworkNeighborsUpdate.RemoveOutdatedNetNeighbors(
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
                        var n = o.BaseObject.Should().BeOfType<CimNetNeighbor>().Subject;
                        n.IpAddress.Should().Be("192.168.1.2");
                    },
                    o =>
                    {
                        var n = o.BaseObject.Should().BeOfType<CimNetNeighbor>().Subject;
                        n.IpAddress.Should().Be("192.168.1.3");
                    }));
    }
}
