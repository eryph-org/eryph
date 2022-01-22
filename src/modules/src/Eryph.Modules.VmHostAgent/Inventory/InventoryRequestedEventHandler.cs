using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    internal class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {
        private readonly IBus _bus;

        private readonly IPowershellEngine _engine;
        private readonly HostInventory _hostInventory;
        private readonly VirtualMachineInventory _inventory;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
            _inventory = new VirtualMachineInventory(_engine, HostSettingsBuilder.GetHostSettings());
            _hostInventory = new HostInventory(_engine);
        }


        public Task Handle(InventoryRequestedEvent message)
        {
            return _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .BindAsync(VmsToInventory)
                .ToAsync().IfRightAsync(c => _bus.Send(c));
        }


        private Task<Either<PowershellFailure, UpdateVMHostInventoryCommand>> VmsToInventory(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return (from vmInventory in vms.Map(_inventory.InventorizeVM).Traverse(l => l)
                    .Map(t => t.Traverse(l => l)).ToAsync()
                from hostInventory in _hostInventory.InventorizeHost().ToAsync()
                select new UpdateVMHostInventoryCommand
                {
                    HostInventory = hostInventory,
                    VMInventory = vmInventory.ToList()
                }).ToEither();
        }
    }
}