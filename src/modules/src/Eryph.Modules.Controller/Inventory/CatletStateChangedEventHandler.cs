using System;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
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
            var vCatlet = await _stateStoreContext.VirtualCatlets.FirstOrDefaultAsync(x=> x.VMId == message.VmId);

            if (vCatlet == null)
                return;

            vCatlet.Status = MapVmStatusToCatletStatus(message.Status);

            if (vCatlet.Status == CatletStatus.Stopped)
            {
                vCatlet.UpTime = TimeSpan.Zero;
            }

            //uow will commit

        }

        private static CatletStatus MapVmStatusToCatletStatus(VmStatus status)
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