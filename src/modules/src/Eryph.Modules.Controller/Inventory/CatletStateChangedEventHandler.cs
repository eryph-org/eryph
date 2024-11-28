using System;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class CatletStateChangedEventHandler(
    IInventoryLockManager lockManager,
    IOperationDispatcher opDispatcher,
    IVirtualMachineMetadataService metadataService,
    IVirtualMachineDataService vmDataService,
    IMessageContext messageContext)
    : IHandleMessages<CatletStateChangedEvent>
{
    public async Task Handle(CatletStateChangedEvent message)
    {
        await lockManager.AcquireVmLock(message.VmId);

        var catletResult = await vmDataService.GetByVMId(message.VmId);
        if (catletResult.IsNone)
            return;

        var catlet = catletResult.ValueUnsafe();
        if (catlet.LastSeenStatus < message.Timestamp)
        {
            catlet.UpTime = message.Status is VmStatus.Stopped ? TimeSpan.Zero : message.UpTime;
            catlet.Status = message.Status.ToCatletStatus();
            catlet.LastSeenStatus = message.Timestamp;
        }

        if (message.UpTime.TotalMinutes < 15)
            return;

        var metadataResult = await metadataService.GetMetadata(catlet.MetadataId);
        if (metadataResult.IsNone)
            // TODO should this be a fail?
            return;

        var metadata = metadataResult.ValueUnsafe();
        
        if (metadata.SecureDataHidden)
            return;

        var anySensitive = metadata.Fodder.ToSeq().Exists(
                               f => f.Secret.GetValueOrDefault()
                                    || f.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault()))
                           || metadata.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault());
        if (!anySensitive)
            return;
        
        metadata.SecureDataHidden = true;
        await metadataService.SaveMetadata(metadata);

        await opDispatcher.StartNew(
            catlet.Project.Id,
            messageContext.GetTraceId(),
            new UpdateConfigDriveCommand
            {
                CatletId = catlet.Id
            });
    }
}
