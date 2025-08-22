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
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory;

[UsedImplicitly]
internal class CatletStateChangedEventHandler(
    IInventoryLockManager lockManager,
    IOperationDispatcher opDispatcher,
    IVirtualMachineMetadataService metadataService,
    IVirtualMachineDataService vmDataService,
    IMessageContext messageContext,
    ILogger logger)
    : IHandleMessages<CatletStateChangedEvent>
{
    public async Task Handle(CatletStateChangedEvent message)
    {
        await lockManager.AcquireVmLock(message.VmId);

        var catletResult = await vmDataService.GetByVMId(message.VmId);
        if (catletResult.IsNone)
            return;

        var catlet = catletResult.ValueUnsafe();
        if (catlet.LastSeenState < message.Timestamp)
        {
            catlet.UpTime = message.Status is VmStatus.Stopped ? TimeSpan.Zero : message.UpTime;
            catlet.Status = message.Status.ToCatletStatus();
            catlet.LastSeenState = message.Timestamp;
        }
        else
        {
            logger.LogDebug("Skipping state update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent state information is dated {LastSeen:O}.",
                catlet.Id, message.Timestamp, catlet.LastSeenState);
        }

        if (message.UpTime.TotalMinutes < 15)
            return;

        var metadata = await metadataService.GetMetadata(catlet.MetadataId);
        if (metadata is null || metadata.SecretDataHidden || metadata.IsDeprecated || metadata.Metadata is null)
            return;

        // TODO is this still correct? How and where do we store the secrets?
        var anySensitive = metadata.Metadata.BuiltConfig.Fodder.ToSeq().Exists(
                               f => f.Secret.GetValueOrDefault()
                                    || f.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault()))
                           || metadata.Metadata.BuiltConfig.Variables.ToSeq().Exists(v => v.Secret.GetValueOrDefault());
        if (!anySensitive)
            return;
        
        await metadataService.MarkSecretDataHidden(catlet.MetadataId);

        await opDispatcher.StartNew(
            catlet.Project.Id,
            messageContext.GetTraceId(),
            new UpdateConfigDriveCommand
            {
                CatletId = catlet.Id
            });
    }
}
