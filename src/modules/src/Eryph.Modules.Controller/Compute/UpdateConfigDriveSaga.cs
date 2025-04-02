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
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

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
            var catlet = await _vmDataService.GetVM(message.CatletId)
                .Map(m => m.IfNoneUnsafe((Catlet?)null));
            if (catlet is null)
            {
                await Fail($"Catlet config drive cannot be updated because the catlet {message.CatletId} does not exist.");
                return;
            }

            var metadata = await _metadataService.GetMetadata(catlet.MetadataId)
                .Map(m => m.IfNoneUnsafe((CatletMetadata?)null));
            if (metadata is null)
            {
                await Fail($"Catlet config drive cannot be updated because the metadata for catlet '{catlet.Name}' ({catlet.Id}) does not exist.");
                return;
            }

            var resolvedFodderGenes = metadata.ResolvedFodderGenes.ToSeq()
                .Map(kvp => from geneId in GeneIdentifier.NewValidation(kvp.Key)
                            from architecture in Architecture.NewValidation(kvp.Value)
                            select new UniqueGeneIdentifier(GeneType.Fodder, geneId, architecture))
                .Sequence();
            if (resolvedFodderGenes.IsFail)
            {
                await Fail(Error.New(
                        $"The metadata for catlet {message.CatletId} contains invalid fodder information",
                        Error.Many(resolvedFodderGenes.FailToSeq()))
                    .Print());
                return;
            }

            // This saga only updates the config drive which is attached to the catlet without
            // updating the catlet itself. We breed a fake catlet config which can be passed
            // to the UpdateCatletConfigDriveCommand to update the config drive.
            // We provide neither Name nor Hostname as the config is only used to update
            // the config drive after the first startup and the hostname should not
            // be changed by cloud-init.
            var config = new CatletConfig()
            {
                Fodder = metadata.Fodder,
                Variables = metadata.Variables,
            };

            var breedingResult = CatletBreeding.Breed(metadata.ParentConfig, config);
            if (breedingResult.IsLeft)
            {
                await Fail(Error.New(
                        $"Could not breed config for catlet '{catlet.Name}' ({catlet.Id}).",
                        Error.Many(breedingResult.LeftToSeq()))
                    .Print());
                return;
            }

            await StartNewTask(new UpdateCatletConfigDriveCommand
            {
                Config = breedingResult.ValueUnsafe(),
                VMId = catlet.VMId,
                CatletId = catlet.Id,
                MachineMetadata = metadata
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