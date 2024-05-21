using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Networking;

public static class NetworkNeighborsUpdate
{
    public static EitherAsync<Error, Unit> RemoveOutdatedNetNeighbors(
        IPowershellEngine powershellEngine,
        Seq<(string IpAddress, string MacAddress)> updatedNeighbors) =>
        from parsedUpdatedNeighbors in updatedNeighbors
            .Map(t => from ip in ParseIpAddress(t.IpAddress)
                      from mac in ParseMacAddress(t.MacAddress)
                      select (IpAddress: ip, MacAddress: mac))
            .Sequence()
            .ToEitherAsync()
        let ipAddresses = updatedNeighbors.Map(a => a.IpAddress).ToArray()
        from psNetNeighbors in powershellEngine.GetObjectsAsync<CimNetNeighbor>(PsCommandBuilder.Create()
                .AddCommand("Get-NetNeighbor")
                .AddParameter("IPAddress", ipAddresses)
                // The Cmdlet will return an error when there is no entry for an
                // IP address. Hence, we need to suppress the error.
                .AddParameter("ErrorAction", "SilentlyContinue"))
            .ToError().ToAsync()
        from psNeighborsToRemove in psNetNeighbors
            .Map(n => from isOutdated in IsOutdated(n, parsedUpdatedNeighbors)
                      select (IsOutdated: isOutdated, Neighbor: n))
            .Sequence()
            .FilterT(t => t.IsOutdated)
            .MapT(t => t.Neighbor)
            .ToEitherAsync()
        from __ in psNeighborsToRemove.Match(
            Empty: () => Prelude.RightAsync<Error, Unit>(Prelude.unit),
            Seq: n => powershellEngine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Remove-NetNeighbor")
                    .AddParameter("InputObject", n.Map(v => v.PsObject).ToArray()))
                .ToError().ToAsync())
        select Prelude.unit;

    private static Fin<bool> IsOutdated(
        CimNetNeighbor psNeighbor,
        Seq<(IPAddress IpAddress, PhysicalAddress MacAddress)> updatedNeighbors) =>
        from ipAddress in ParseIpAddress(psNeighbor.IpAddress)
        from macAddress in ParseMacAddress(psNeighbor.LinkLayerAddress)
        select updatedNeighbors
            .Find(updatedNeighbor => updatedNeighbor.IpAddress.Equals(ipAddress))
            .Map(updatedNeighbor => !updatedNeighbor.MacAddress.Equals(macAddress))
            .IfNone(false);

    private static Fin<IPAddress> ParseIpAddress(string value) =>
        IPAddress.TryParse(value, out var ipAddress)
            ? ipAddress
            : Error.New($"The IP address '{value}' is invalid");

    private static Fin<PhysicalAddress> ParseMacAddress(string value) =>
        PhysicalAddress.TryParse(value, out var macAddress)
            ? macAddress
            : Error.New($"The MAC address '{value}' is invalid");
}
