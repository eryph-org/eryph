using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    internal class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;

        private readonly IPowershellEngine _engine;
        private readonly HostInventory _hostInventory;
        private readonly VirtualMachineInventory _inventory;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine, ILogger log)
        {
            _bus = bus;
            _engine = engine;
            _log = log;
            _inventory = new VirtualMachineInventory(_engine, HostSettingsBuilder.GetHostSettings());
            _hostInventory = new HostInventory(_engine, log);
        }


        public Task Handle(InventoryRequestedEvent message)
        {
            return _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .BindAsync(VmsToInventory)
                .ToAsync()
                .MatchAsync(
                    RightAsync: c => _bus.Send(c),
                    Left: l => { _log.LogError(l.Message); }
                );
        }


        private Task<Either<PowershellFailure, UpdateVMHostInventoryCommand>> VmsToInventory(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return  
                (from hostInventory in _hostInventory.InventorizeHost().ToAsync()
                from vmInventory in InventorizeAllVms(vms).ToAsync()
                select new UpdateVMHostInventoryCommand
                {
                    HostInventory = hostInventory,
                    VMInventory = vmInventory.ToList()
                }).ToEither();
        }

        private Task<Either<PowershellFailure, IEnumerable<VirtualMachineData>>> InventorizeAllVms(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return
                vms.Map(vm => _inventory.InventorizeVM(vm)
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
                    .Map(Prelude.Right<PowershellFailure, IEnumerable<VirtualMachineData>>);

        }
    }
}