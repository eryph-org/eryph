using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Networking;

public static class NetworkNeighborsUpdate
{
    public static EitherAsync<Error, Unit> RemoveOutdatedNetworkNeighbors(
        IPowershellEngine powershellEngine,
        Seq<(string IpAddress, string MacAddress)> updatedNeighbors) =>
        from parsedUpdatedNeighbors in updatedNeighbors
            .Map(t => from ip in ParseIpAddress(t.IpAddress)
                      from mac in Optional(t.MacAddress)
                          .Filter(notEmpty)
                          .Map(ParseMacAddress)
                          .Sequence()
                      select (IpAddress: ip, MacAddress: mac))
            .Sequence()
            .ToEitherAsync()
        let ipAddresses = updatedNeighbors.Map(a => a.IpAddress).ToArray()
        from psNetNeighbors in powershellEngine.GetObjectsAsync<CimNetworkNeighbor>(PsCommandBuilder.Create()
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
            Empty: () => RightAsync<Error, Unit>(unit),
            Seq: n => powershellEngine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Remove-NetNeighbor")
                    .AddParameter("InputObject", n.Map(v => v.PsObject).ToArray())
                    .AddParameter("Confirm", false))
                .ToError().ToAsync())
        select unit;

    private static Fin<bool> IsOutdated(
        CimNetworkNeighbor psNeighbor,
        Seq<(IPAddress IpAddress, Option<PhysicalAddress> MacAddress)> updatedNeighbors) =>
        from ipAddress in ParseIpAddress(psNeighbor.IPAddress)
        from macAddress in ParseMacAddress(psNeighbor.LinkLayerAddress)
        select updatedNeighbors
            .Find(updatedNeighbor => updatedNeighbor.IpAddress.Equals(ipAddress))
            .Map(updatedNeighbor => !updatedNeighbor.MacAddress.Equals(Some(macAddress)))
            .IfNone(false);

    private static Fin<IPAddress> ParseIpAddress(string value) =>
        IPAddress.TryParse(value, out var ipAddress)
            ? ipAddress
            : Error.New($"The IP address '{value}' is invalid");

    private static Fin<PhysicalAddress> ParseMacAddress(string value) =>
        !string.IsNullOrWhiteSpace(value) && PhysicalAddress.TryParse(value, out var macAddress)
            ? macAddress
            : Error.New($"The MAC address '{value}' is invalid");
}
