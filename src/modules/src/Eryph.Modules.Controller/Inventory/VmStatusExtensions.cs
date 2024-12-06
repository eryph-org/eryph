using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
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
            _ => throw new ArgumentOutOfRangeException(nameof(vmStatus), vmStatus,
                $"The status {vmStatus} is not supported"),
        };
}
