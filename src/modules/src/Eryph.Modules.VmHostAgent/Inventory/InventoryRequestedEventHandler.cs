using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
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

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    internal class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;

        private readonly IPowershellEngine _engine;
        private readonly IHostInfoProvider _hostInfoProvider;
        private readonly VirtualMachineInventory _inventory;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine, ILogger log, IHostInfoProvider hostInfoProvider)
        {
            _bus = bus;
            _engine = engine;
            _log = log;
            _hostInfoProvider = hostInfoProvider;
            _inventory = new VirtualMachineInventory(_engine, HostSettingsBuilder.GetHostSettings(), hostInfoProvider);
        }


        public Task Handle(InventoryRequestedEvent message)
        {
            return _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm")).ToError()
                .BindAsync(VmsToInventory)
                .ToAsync()
                .MatchAsync(
                    RightAsync: c => _bus.Send(c),
                    Left: l =>
                    {
                        _log.LogError(l.Message);
                    }
                );
        }


        private Task<Either<Error, UpdateVMHostInventoryCommand>> VmsToInventory(
            Seq<TypedPsObject<VirtualMachineInfo>> vms)
        {
            return  
                (from hostInventory in _hostInfoProvider.GetHostInfoAsync(true)
                from vmInventory in InventorizeAllVms(vms).ToAsync()
                select new UpdateVMHostInventoryCommand
                {
                    HostInventory = hostInventory,
                    VMInventory = vmInventory.ToList()
                }).ToEither();
        }

        private Task<Either<Error, IEnumerable<VirtualMachineData>>> InventorizeAllVms(
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
                    .Map(Prelude.Right<Error, IEnumerable<VirtualMachineData>>);

        }
    }
}