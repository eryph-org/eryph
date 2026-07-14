using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.GuestServices.HvDataExchange.Host;
using Eryph.Resources.Machines;

namespace Eryph.Modules.HostAgent.Inventory;

public interface IProvisioningStateReader
{
    /// <summary>
    /// Reads the current provisioning status of the guest from its Hyper-V KVP
    /// pool. Returns <see cref="ProvisioningStatus.Unknown"/> when guest-services
    /// has not reported a provisioning state (e.g. the VM is not running or does
    /// not run guest-services).
    /// </summary>
    Task<ProvisioningStatus> ReadAsync(Guid vmId);
}

/// <summary>
/// Reads the single <c>eryph.provisioning.state</c> KVP value written by the
/// guest (egs on Windows, the cloud-init status watcher on Linux) and maps it to
/// <see cref="ProvisioningStatus"/>.
/// </summary>
public sealed class ProvisioningStateReader(IHostDataExchange hostDataExchange)
    : IProvisioningStateReader
{
    public async Task<ProvisioningStatus> ReadAsync(Guid vmId)
    {
        var guest = await hostDataExchange.GetGuestDataAsync(vmId).ConfigureAwait(false);
        return ProvisioningStateMapper.Map(
            guest.GetValueOrDefault(ProvisioningStateMapper.ProvisioningStateKey));
    }
}

/// <summary>
/// Maps the guest-written <c>eryph.provisioning.state</c> string to
/// <see cref="ProvisioningStatus"/>. The producer values are defined by
/// guest-services (egs <c>KvpReportingHandler</c>).
/// </summary>
public static class ProvisioningStateMapper
{
    // The KVP key the guest writes the coarse provisioning state to. Not a
    // published guest-services constant, so it is spelled out here (and in
    // GuestStatusReader).
    public const string ProvisioningStateKey = "eryph.provisioning.state";

    public static ProvisioningStatus Map(string? value) => value switch
    {
        "started" => ProvisioningStatus.Started,
        "running" => ProvisioningStatus.Running,
        "reboot_pending" => ProvisioningStatus.RebootPending,
        "completed" => ProvisioningStatus.Completed,
        "failed" => ProvisioningStatus.Failed,
        _ => ProvisioningStatus.Unknown,
    };
}
