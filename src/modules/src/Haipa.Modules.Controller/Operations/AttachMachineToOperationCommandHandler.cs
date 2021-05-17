using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Operations
{
    //[UsedImplicitly]
    //public class AttachMachineToOperationCommandHandler : IHandleMessages<AttachMachineToOperationCommand>
    //{
    //    private readonly StateStoreContext _dbContext;

    //    public AttachMachineToOperationCommandHandler(StateStoreContext dbContext)
    //    {
    //        _dbContext = dbContext;
    //    }

    //    public async Task Handle(AttachMachineToOperationCommand message)
    //    {
    //        var operation = _dbContext.Operations.Include(x=>x.Resources)
    //            .FirstOrDefault(op => op.Id == message.OperationId);

    //        if (operation != null && message.MachineId != 0)
    //        {
    //            if(operation.Resources.All(x => x.ResourceId != message.MachineId))
    //                operation.Resources.Add(new OperationResource
    //                {
    //                    Id = Guid.NewGuid(),
    //                    ResourceId = message.MachineId, 
    //                    ResourceType = ResourceType.Machine
    //                });
    //        }


    //        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

    //    }

    //}
}