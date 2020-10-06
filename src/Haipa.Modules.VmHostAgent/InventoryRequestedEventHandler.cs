using System;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Events;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal partial class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {

        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;
        private readonly VirtualMachineInventory _inventory;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
            _inventory = new VirtualMachineInventory();
        }


        public Task Handle(InventoryRequestedEvent message) =>
            _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .BindAsync(VmsToInventory)
                .ToAsync().IfRightAsync(c => _bus.Send(c));

        private Task<Either<PowershellFailure, UpdateInventoryCommand>> VmsToInventory(Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return vms.Map(_inventory.InventorizeVM).Traverse(l => l)
                .Map(t => t.Traverse(l => l))
                .BindAsync(s =>
                    Prelude.RightAsync<PowershellFailure,UpdateInventoryCommand>(new UpdateInventoryCommand
                    {
                        AgentName = Environment.MachineName,
                        Inventory = s.ToList()

                    }).ToEither()
                );

        }


    }
}