using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using Rebus.Handlers;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

using static OvsPortCommands<AgentRuntime>;

internal class SyncVmNetworkPortsCommandHandler(
    Scope serviceScope,
    ITaskMessaging messaging)
    : IHandleMessages<OperationTask<SyncVmNetworkPortsCommand>>
{
    public async Task Handle(OperationTask<SyncVmNetworkPortsCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    private static Aff<AgentRuntime, Unit> HandleCommand(
        SyncVmNetworkPortsCommand command) =>
        from psEngine in default(AgentRuntime).Powershell
        from vmInfo in VmQueries.GetOptionalVmInfo(psEngine, command.VMId).ToAff()
        from _ in vmInfo
            // This command is only used to sync the ports when the network adapters
            // of a running VM have been modified. A different event and handler
            // sync the ports when a VM is started or stopped.
            .Filter(v => v.Value.State is VirtualMachineState.Running or VirtualMachineState.RunningCritical)
            .Map(v => syncOvsPorts(v, VMPortChange.Add))
            .SequenceSerial()
        select unit;
}
