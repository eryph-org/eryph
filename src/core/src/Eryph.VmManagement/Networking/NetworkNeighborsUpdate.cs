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
        from _ in Prelude.RightAsync<Error, Unit>(Prelude.unit)
        let ipAddresses = updatedNeighbors.Map(a => a.IpAddress).ToArray()
        from psNetNeighbors in powershellEngine.GetObjectsAsync<CimNetNeighbor>(PsCommandBuilder.Create()
                .AddCommand("Get-NetNeighbor")
                .AddParameter("IPAddress", ipAddresses)
                // The Cmdlet will return an error when there is no entry for an
                // IP address. Hence, we need to suppress the error.
                .AddParameter("ErrorAction", "SilentlyContinue"))
            .ToError().ToAsync()
        let psNeighborsToRemove = psNetNeighbors
            .Filter(n => IsOutdated(n, updatedNeighbors).IfNone(false))
        from __ in psNeighborsToRemove.Match(
            Empty: () => Prelude.RightAsync<Error, Unit>(Prelude.unit),
            Seq: n => powershellEngine.RunAsync(PsCommandBuilder.Create()
                    .AddCommand("Remove-NetNeighbor")
                    .AddParameter("InputObject", n.Map(v => v.PsObject).ToArray()))
                .ToError().ToAsync())
        select Prelude.unit;

    private static Option<bool> IsOutdated(
        CimNetNeighbor psNeighbor,
        Seq<(string IpAddress, string MacAddress)> updatedNeighbors) =>
        from ipAddress in parseIpAddress(psNeighbor.IpAddress)
        from macAddress in parseMacAddress(psNeighbor.LinkLayerAddress)
        from updatedNeighbor in updatedNeighbors.Find(n =>
            parseIpAddress(n.IpAddress).Map(a => a.Equals(ipAddress)).IfNone(false))
        select parseMacAddress(updatedNeighbor.MacAddress).Map(a => !a.Equals(macAddress)).IfNone(false);

    private static Option<IPAddress> parseIpAddress(string value) =>
        IPAddress.TryParse(value, out var ip) ? ip : Option<IPAddress>.None;

    private static Option<PhysicalAddress> parseMacAddress(string value) =>
        PhysicalAddress.TryParse(value, out var ip) ? ip : Option<PhysicalAddress>.None;
}
