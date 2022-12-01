using System;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Events;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory
{
    internal class CatletStateChangedEventHandler : IHandleMessages<VMStateChangedEvent>
    {
        private readonly StateStoreContext _stateStoreContext;

        public CatletStateChangedEventHandler(StateStoreContext stateStoreContext)
        {
            _stateStoreContext = stateStoreContext;
        }

        public async Task Handle(VMStateChangedEvent message)
        {
            var vm = await _stateStoreContext.VirtualCatlets.FirstOrDefaultAsync(x=> x.VMId == message.VmId);

            if (vm == null)
                return;

            vm.Status = MapVmStatusToMachineStatus(message.Status);

            if (vm.Status == CatletStatus.Stopped)
            {
                vm.UpTime = TimeSpan.Zero;
            }


        }

        private static CatletStatus MapVmStatusToMachineStatus(VmStatus status)
        {
            switch (status)
            {
                case VmStatus.Stopped:
                    return CatletStatus.Stopped;
                case VmStatus.Pending:
                    return CatletStatus.Pending;
                case VmStatus.Error:
                    return CatletStatus.Error;
                case VmStatus.Running:
                    return CatletStatus.Running;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}