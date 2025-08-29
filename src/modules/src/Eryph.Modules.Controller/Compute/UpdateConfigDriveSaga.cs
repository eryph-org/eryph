using System.Threading.Tasks;
using Dbosoft.Functional;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class UpdateConfigDriveSaga :
        OperationTaskWorkflowSaga<UpdateConfigDriveCommand, UpdateConfigDriveSagaData>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualMachineMetadataService _metadataService;
        public UpdateConfigDriveSaga(IWorkflow workflow, IVirtualMachineDataService vmDataService, IVirtualMachineMetadataService metadataService) 
            : base(workflow)
        {
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        protected override async Task Initiated(UpdateConfigDriveCommand message)
        {
            var catlet = await _vmDataService.Get(message.CatletId)
                .Map(m => m.IfNoneUnsafe((Catlet?)null));
            if (catlet is null)
            {
                await Fail($"Catlet config drive cannot be updated because the catlet {message.CatletId} does not exist.");
                return;
            }

            var metadata = await _metadataService.GetMetadata(catlet.MetadataId);
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

            // TODO feed system variables?
            // TODO feed variables from metadata
            // TODO Hide secret data already here

            await StartNewTask(new UpdateCatletConfigDriveCommand
            {
                Config = metadata.Metadata.BuiltConfig,
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
}