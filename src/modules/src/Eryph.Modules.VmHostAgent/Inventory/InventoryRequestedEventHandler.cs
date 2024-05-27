using System;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    internal class InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine, ILogger log,
            WorkflowOptions workflowOptions,
            IHostInfoProvider hostInfoProvider,
            IHostSettingsProvider hostSettingsProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
        : IHandleMessages<InventoryRequestedEvent>
    {
        public Task Handle(InventoryRequestedEvent message)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm")).ToError()
                .BindAsync(VmsToInventory)
                .ToAsync()
                .MatchAsync(
                    RightAsync: c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
                    Left: l =>
                    {
                        log.LogError(l.Message);
                    }
                );
        }


        private Task<Either<Error, UpdateVMHostInventoryCommand>> VmsToInventory(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return  
                (from hostInventory in hostInfoProvider.GetHostInfoAsync(true) 
                 let timestamp = DateTimeOffset.UtcNow
                 from hostSettings in hostSettingsProvider.GetHostSettings()
                 from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
                 let inventory = new VirtualMachineInventory(engine, vmHostAgentConfig, hostInfoProvider)
                 from vmInventory in InventorizeAllVms(inventory, vms).ToAsync()
                select new UpdateVMHostInventoryCommand
                {
                    HostInventory = hostInventory,
                    VMInventory = vmInventory.ToList(),
                    Timestamp = timestamp
                }).ToEither();
        }

        private Task<Either<Error, IEnumerable<VirtualMachineData>>> InventorizeAllVms(
            VirtualMachineInventory inventory,
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return
                vms.Map(vm => inventory.InventorizeVM(vm)
                        .ToAsync().Match(
                            Right: Prelude.Some,
                            Left: l =>
                            {
                                log.LogError(
                                    "Inventory of virtual machine '{VMName}' (Id:{VmId}) failed. Error: {failure}",
                                    vm.Value.Name, vm.Value.Id, l.Message);
                                return Prelude.None;
                            })

                    )
                    .TraverseParallel(l => l.AsEnumerable())
                    .Map(seq => seq.Flatten())
                    .Map(Prelude.Right<Error, IEnumerable<VirtualMachineData>>);

        }
    }
}