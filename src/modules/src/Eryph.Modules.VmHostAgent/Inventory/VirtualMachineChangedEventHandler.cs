﻿using System;
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

        result.IfFail(e => { logger.LogError(e, "Inventory of VM {VmId} failed", message.VmId); });
        if (result.IsFail)
            return;

        await result.ToOption().Flatten()
            .IfSomeAsync(c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c));
    }

    private Aff<Option<UpdateInventoryCommand>> Handle(Guid vmId) =>
        from _ in SuccessAff(unit)
        let timestamp = DateTimeOffset.UtcNow
        from hostSettings in hostSettingsProvider.GetHostSettings()
            .ToAff(identity)
        from vmHostAgentConfig in vmHostAgentConfigManager.GetCurrentConfiguration(hostSettings)
            .ToAff(identity)
        let inventory = new VirtualMachineInventory(powershellEngine, vmHostAgentConfig, hostInfoProvider)
        from vmInfo in VmQueries.GetVmInfo(powershellEngine, vmId).ToAff()
        let inventorizableVmInfo = Optional(vmInfo).Filter(IsInventorizable)
        from vmData in inventorizableVmInfo
            .Map(vm => inventory.InventorizeVM(vm).ToAff(identity))
            .Sequence()
        select vmData.Map(data => new UpdateInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = data,
        });

    private bool IsInventorizable(TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var operationalStatus = VmStateUtils.convertMsvmOperationalStatus(vmInfo.Value.OperationalStatus);
        var state = vmInfo.Value.State;
        var isInventorizable = VmStateUtils.isInventorizable(state, operationalStatus);

        if (!isInventorizable)
        {
            logger.LogInformation("Skipping inventory of VM {VmId} because of its status: {State}, {OperationalStatus}",
                vmInfo.Value.Id, state, operationalStatus);
        }

        return isInventorizable;
    }
}
