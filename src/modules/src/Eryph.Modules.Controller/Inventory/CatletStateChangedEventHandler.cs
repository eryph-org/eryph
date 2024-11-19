using System;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory;

internal class CatletStateChangedEventHandler : IHandleMessages<VMStateChangedEvent>
{
    private readonly StateStoreContext _stateStoreContext;

    public CatletStateChangedEventHandler(StateStoreContext stateStoreContext)
    {
        _stateStoreContext = stateStoreContext;
    }

    public async Task Handle(VMStateChangedEvent message)
    {
        // TODO add locking
        var catlet = await _stateStoreContext.Catlets.FirstOrDefaultAsync(x=> x.VMId == message.VmId);

        if (catlet == null)
            return;

        // ignore old events
        if(catlet.StatusTimestamp > message.Timestamp) 
            return;

        catlet.Status = MapVmStatusToCatletStatus(message.Status);
        // TODO Fix me
        catlet.StatusTimestamp = message.Timestamp.UtcDateTime;
        if (catlet.Status == CatletStatus.Stopped)
        {
            catlet.UpTime = TimeSpan.Zero;
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
