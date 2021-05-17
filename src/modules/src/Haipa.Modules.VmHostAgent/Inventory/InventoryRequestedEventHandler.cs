using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Messages.Resources.Machines.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using VirtualMachineInfo = Haipa.VmManagement.Data.Full.VirtualMachineInfo;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal partial class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {

        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;
        private readonly VirtualMachineInventory _inventory;
        private readonly HostInventory _hostInventory;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
            _inventory = new VirtualMachineInventory(_engine, HostSettingsBuilder.GetHostSettings());
            _hostInventory = new HostInventory(_engine);
        }


        public Task Handle(InventoryRequestedEvent message) =>
            _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .BindAsync(VmsToInventory)
                .ToAsync().IfRightAsync(c => _bus.Send(c));


        private Task<Either<PowershellFailure, UpdateVMHostInventoryCommand>> VmsToInventory(Seq<TypedPsObject<VirtualMachineInfo>> vms)
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