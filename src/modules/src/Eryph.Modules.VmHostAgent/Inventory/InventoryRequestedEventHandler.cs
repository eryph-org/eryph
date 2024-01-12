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
    internal class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;
        private readonly WorkflowOptions _workflowOptions;

        private readonly IPowershellEngine _engine;
        private readonly IHostInfoProvider _hostInfoProvider;
        private readonly IVmHostAgentConfigurationManager _vmHostAgentConfigurationManager;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine, ILogger log,
            WorkflowOptions _workflowOptions,
            IHostInfoProvider hostInfoProvider,
            IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
        {
            _bus = bus;
            _engine = engine;
            _log = log;
            this._workflowOptions = _workflowOptions;
            _hostInfoProvider = hostInfoProvider;
            _vmHostAgentConfigurationManager = vmHostAgentConfigurationManager;
        }


        public Task Handle(InventoryRequestedEvent message)
        {
            return _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm")).ToError()
                .BindAsync(VmsToInventory)
                .ToAsync()
                .MatchAsync(
                    RightAsync: c => _bus.Advanced.Routing.Send(_workflowOptions.OperationsDestination, c),
                    Left: l =>
                    {
                        _log.LogError(l.Message);
                    }
                );
        }


        private Task<Either<Error, UpdateVMHostInventoryCommand>> VmsToInventory(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            var hostSettings = HostSettingsBuilder.GetHostSettings();

            return  
                (from hostInventory in _hostInfoProvider.GetHostInfoAsync(true) 
                 from vmHostAgentConfig in _vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
                 let inventory = new VirtualMachineInventory(_engine, vmHostAgentConfig, hostSettings, _hostInfoProvider)
                 from vmInventory in InventorizeAllVms(inventory, vms).ToAsync()
                select new UpdateVMHostInventoryCommand
                {
                    HostInventory = hostInventory,
                    VMInventory = vmInventory.ToList()
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
                                _log.LogError(
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