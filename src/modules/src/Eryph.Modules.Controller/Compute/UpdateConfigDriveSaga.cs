using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class UpdateConfigDriveSaga(
    IWorkflow workflow,
    ICatletDataService vmDataService,
    ICatletMetadataService metadataService) :
    OperationTaskWorkflowSaga<UpdateConfigDriveCommand, UpdateConfigDriveSagaData>(workflow),
    IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>
{
    protected override async Task Initiated(UpdateConfigDriveCommand message)
    {
        var catlet = await vmDataService.Get(message.CatletId);
        if (catlet is null)
        {
            await Fail($"Catlet config drive cannot be updated because the catlet {message.CatletId} does not exist.");
            return;
        }

        var metadata = await metadataService.GetMetadata(catlet.MetadataId);
        if (metadata is null)
        {
            await Fail($"Catlet config drive cannot be updated because the metadata for catlet '{catlet.Name}' ({catlet.Id}) does not exist.");
            return;
        }

        if (metadata.IsDeprecated || metadata.Metadata is null)
        {
            await Fail($"Catlet config drive cannot be updated because the catlet '{catlet.Name}' ({catlet.Id}) has been created with an old version of eryph.");
            return;
        }

        await StartNewTask(new UpdateCatletConfigDriveCommand
        {
            Config = CatletSystemDataFeeding.FeedSystemVariables(
                metadata.Metadata.BuiltConfig, catlet.Id, catlet.VmId),
            VmId = catlet.VmId,
            CatletId = catlet.Id,
            MetadataId = catlet.MetadataId,
            SecretDataHidden = metadata.SecretDataHidden,
        });
    }

    public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
    {
        return FailOrRun(message, Complete);
    }

    protected override void CorrelateMessages(ICorrelationConfig<UpdateConfigDriveSagaData> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(m => m.InitiatingTaskId, m => m.SagaTaskId);
    }
}
