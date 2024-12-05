using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
internal class VirtualMachineChangedEventHandler(
    IBus bus,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    ILogger logger,
    IPowershellEngine powershellEngine,
    IVmHostAgentConfigurationManager vmHostAgentConfigManager,
    WorkflowOptions workflowOptions)
    : IHandleMessages<VirtualMachineChangedEvent>
{
    public async Task Handle(VirtualMachineChangedEvent message)
    {
        var result = await Handle(message.VmId).Run();

        result.Match(
            Succ: c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
            Fail: e => logger.LogError(e, "Inventory of VM {VmId} failed", message.VmId));
    }

    private Aff<UpdateInventoryCommand> Handle(Guid vmId) =>
        from _ in SuccessEff(unit)
        let timestamp = DateTimeOffset.UtcNow
        from hostSettings in hostSettingsProvider.GetHostSettings()
            .ToAff(identity)
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
            .ToAff(identity)
        let inventory = new VirtualMachineInventory(powershellEngine, vmHostAgentConfig, hostInfoProvider)
        let getVmCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", vmId)
        from vmInfos in powershellEngine.GetObjectsAsync<VirtualMachineInfo>(getVmCommand)
            .ToAff()
        from vmInfo in vmInfos.HeadOrNone()
            .ToAff(Error.New($"The VM with ID {vmId} was not found"))
        from vmData in inventory.InventorizeVM(vmInfo).ToAff(identity)
        select new UpdateInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = [vmData],
        };
}