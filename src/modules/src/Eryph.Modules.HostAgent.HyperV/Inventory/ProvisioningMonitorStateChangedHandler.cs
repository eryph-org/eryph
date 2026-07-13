using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using Rebus.Handlers;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent.Inventory;

/// <summary>
/// Enrolls a catlet into the <see cref="ProvisioningStateMonitor"/> when it
/// enters the running state (its first boot) and drops it again when it leaves
/// the running state. Runs alongside the other
/// <see cref="VirtualMachineStateChangedEvent"/> handlers.
/// </summary>
[UsedImplicitly]
internal class ProvisioningMonitorStateChangedHandler(
    IProvisioningStateMonitor monitor)
    : IHandleMessages<VirtualMachineStateChangedEvent>
{
    public Task Handle(VirtualMachineStateChangedEvent message)
    {
        var status = VmStateUtils.toVmStatus(Optional(message.State));
        if (status is VmStatus.Running)
            monitor.Track(message.VmId);
        else
            monitor.Untrack(message.VmId);

        return Task.CompletedTask;
    }
}
