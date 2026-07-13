using System;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Inventory;

internal static class VmStatusExtensions
{
    public static CatletStatus ToCatletStatus(this VmStatus vmStatus) =>
        vmStatus switch
        {
            VmStatus.Error => CatletStatus.Error,
            VmStatus.Pending => CatletStatus.Pending,
            VmStatus.Running => CatletStatus.Running,
            VmStatus.Stopped => CatletStatus.Stopped,
            VmStatus.Unknown => CatletStatus.Unknown,
            VmStatus.Missing => CatletStatus.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(vmStatus), vmStatus,
                $"The status {vmStatus} is not supported"),
        };

    public static CatletProvisioningStatus ToCatletProvisioningStatus(
        this ProvisioningStatus provisioningStatus) =>
        provisioningStatus switch
        {
            ProvisioningStatus.Unknown => CatletProvisioningStatus.Unknown,
            ProvisioningStatus.Started => CatletProvisioningStatus.Started,
            ProvisioningStatus.Running => CatletProvisioningStatus.Running,
            ProvisioningStatus.RebootPending => CatletProvisioningStatus.RebootPending,
            ProvisioningStatus.Completed => CatletProvisioningStatus.Completed,
            ProvisioningStatus.Failed => CatletProvisioningStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(provisioningStatus), provisioningStatus,
                $"The provisioning status {provisioningStatus} is not supported"),
        };
}
