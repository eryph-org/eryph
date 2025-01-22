using Dbosoft.Rebus.Operations.Events;
using Rebus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class CheckDisksExistsReplyHandler(
    IWorkflow workflow,
    IStateStoreRepository<VirtualDisk> repository,
    IInventoryLockManager lockManager)
    : IHandleMessages<OperationTaskStatusEvent<CheckDisksExistsCommand>>
{
    public async Task Handle(OperationTaskStatusEvent<CheckDisksExistsCommand> message)
    {
        if (message.OperationFailed)
            return;

        if (message.GetMessage(workflow.WorkflowOptions.JsonSerializerOptions) is not CheckDisksExistsReply reply)
            return;

        if (reply.MissingDisks is not { Count: > 0 })
            return;

        // Acquire all necessary locks in the beginning to minimize the potential for deadlocks.
        foreach (var diskIdentifier in reply.MissingDisks.Map(d => d.DiskIdentifier).Order())
        {
            await lockManager.AcquireVhdLock(diskIdentifier);
        }

        foreach (var diskInfo in reply.MissingDisks)
        {
            var disk = await repository.GetByIdAsync(diskInfo.Id);
            if (disk is not null)
            {
                disk.Deleted = true;
                disk.LastSeen = reply.Timestamp;
            }
        }
    }
}
