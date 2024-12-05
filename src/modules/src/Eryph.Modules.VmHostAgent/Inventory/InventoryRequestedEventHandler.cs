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
using LanguageExt.Common;
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
        from validVmInfos in vmInfos.Map(CanBeInventoried)
            .Sequence().ToAff()
            .Map(s => s.Somes())
        from hostInventory in hostInfoProvider.GetHostInfoAsync(true).ToAff(identity)
        from hostSettings in hostSettingsProvider.GetHostSettings().ToAff(identity)
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            .ToAff(identity)
        let inventory = new VirtualMachineInventory(engine, vmHostAgentConfig, hostInfoProvider)
        from vmData in validVmInfos
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
                           log.LogError(e, "Inventory of virtual machine '{VmName}' (Id:{VmId}) failed.",
                               vmInfo.Value.Name, vmInfo.Value.Id);
                           return SuccessAff(Option<VirtualMachineData>.None);
                       })
        select vmData;

    private Eff<Option<TypedPsObject<VirtualMachineInfo>>> CanBeInventoried(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from vmInfoValue in Eff(() => Some(vmInfo.Value))
                            | @catch(e =>
                            {
                                log.LogError(e, "Failed to extract VM information from Powershell response");
                                return SuccessEff(Option<VirtualMachineInfo>.None);
                            })
        let operationalStatus = vmInfoValue.Bind(v => OperationalStatusConverter.Convert(v.OperationalStatus))
        let state = vmInfoValue.Map(v => v.State)
        let canBeInventoried = VmStateUtils.canBeInventoried(state, operationalStatus)
        // When Powershell data is invalid, we already logged that above. Also,
        // we do not have any ID which we could log in that case.
        let _ = vmInfoValue.Filter(_ => !canBeInventoried).IfSome(v =>
        {
            log.LogInformation("Skipping inventory of VM {VmId} because of its status: {State}, {OperationalStatus}.",
                v.Id, state, operationalStatus);
        })
        select canBeInventoried ? Some(vmInfo) : None;
}
