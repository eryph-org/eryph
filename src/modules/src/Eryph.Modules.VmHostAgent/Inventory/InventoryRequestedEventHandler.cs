using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
internal class InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine, ILogger log,
    WorkflowOptions workflowOptions,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : IHandleMessages<InventoryRequestedEvent>
{
    public async Task Handle(InventoryRequestedEvent message)
    {
        var result = await InventoryAllVms().Run();

        result.IfFail(e => { log.LogError(e, "The inventory has failed."); });
        if (result.IsFail)
            return;

        await bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, result.ToOption().ValueUnsafe());
    }

    private Aff<UpdateVMHostInventoryCommand> InventoryAllVms() =>
        from _ in SuccessAff(unit)
        let timestamp = DateTimeOffset.UtcNow
        let psCommand = PsCommandBuilder.Create().AddCommand("Get-VM")
        from vmInfos in engine.GetObjectsAsync<VirtualMachineInfo>(
                PsCommandBuilder.Create().AddCommand("Get-VM"))
            .ToAff()
        let inventorizableVmInfos = vmInfos.Filter(IsInventorizable)
        from hostInventory in hostInfoProvider.GetHostInfoAsync(true).ToAff(identity)
        from hostSettings in hostSettingsProvider.GetHostSettings().ToAff(identity)
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            .ToAff(identity)
        let inventory = new VirtualMachineInventory(engine, vmHostAgentConfig, hostInfoProvider)
        from vmData in inventorizableVmInfos
            .Map(vmInfo => InventoryVm(inventory, vmInfo))
            .SequenceParallel()
        select new UpdateVMHostInventoryCommand
        {
            HostInventory = hostInventory,
            VMInventory = vmData.Somes().ToList(),
            Timestamp = timestamp
        };

    private Aff<Option<VirtualMachineData>> InventoryVm(
        VirtualMachineInventory inventory,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from vmData in inventory.InventorizeVM(vmInfo).ToAff(identity).Map(Some)
                       | @catch(e =>
                       {
                           log.LogError(e, "Inventory of virtual machine '{VmName}' (Id:{VmId}) failed",
                               vmInfo.Value.Name, vmInfo.Value.Id);
                           return SuccessAff(Option<VirtualMachineData>.None);
                       })
        select vmData;

    private bool IsInventorizable(TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var operationalStatus = VmStateUtils.Convert(vmInfo.Value.OperationalStatus);
        var state = vmInfo.Value.State;
        var isInventorizable = VmStateUtils.isInventorizable(state, operationalStatus);
        
        if (!isInventorizable)
        {
            log.LogInformation("Skipping inventory of VM {VmId} because of its status: {State}, {OperationalStatus}",
                vmInfo.Value.Id, state, operationalStatus);
        }

        return isInventorizable;
    }
}
