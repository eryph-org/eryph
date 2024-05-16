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
using Eryph.Resources.Disks;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class CheckDisksExistsReplyHandler(
    IWorkflow workflow,
    IVirtualDiskDataService dataService)
    : IHandleMessages<OperationTaskStatusEvent<CheckDisksExistsCommand>>
{
    public async Task Handle(OperationTaskStatusEvent<CheckDisksExistsCommand> message)
    {
        if (message.OperationFailed)
            return;

        if (message.GetMessage(workflow.WorkflowOptions.JsonSerializerOptions) is not CheckDisksExistsReply reply)
            return;

        foreach (var disk in reply.MissingDisks ?? Array.Empty<DiskInfo>())
        {
            await dataService.DeleteVHD(disk.Id);
        }
    }
}
