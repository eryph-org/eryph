using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.GuestServices.Core;
using Eryph.GuestServices.HvDataExchange.Host;

namespace Eryph.Modules.HostAgent;

public sealed record GuestStatus(
    string? GuestServicesStatus,
    string? GuestServicesVersion,
    string? ProvisioningState,
    string? Shell);

public interface IGuestStatusReader
{
    Task<GuestStatus> ReadAsync(Guid vmId);
}

/// <summary>
/// Reads the guest's status from its Hyper-V KVP pool: the guest-services agent
/// status/version and the single provisioning-state value.
/// </summary>
public sealed class GuestStatusReader(IHostDataExchange hostDataExchange) : IGuestStatusReader
{
    // The single provisioning-state value written by the guest (egs on Windows,
    // the cloud-init status watcher on Linux). Not a published guest-services
    // constant, so the key is spelled out here.
    private const string ProvisioningStateKey = "eryph.provisioning.state";

    public async Task<GuestStatus> ReadAsync(Guid vmId)
    {
        // Status/version/provisioning are in the Guest pool (guest -> host); the
        // shell override is in the External pool (host -> guest).
        var guest = await hostDataExchange.GetGuestDataAsync(vmId).ConfigureAwait(false);
        var external = await hostDataExchange.GetExternalDataAsync(vmId).ConfigureAwait(false);
        return new GuestStatus(
            guest.GetValueOrDefault(Constants.StatusKey),
            guest.GetValueOrDefault(Constants.VersionKey),
            guest.GetValueOrDefault(ProvisioningStateKey),
            external.GetValueOrDefault(Constants.ShellKey));
    }
}
