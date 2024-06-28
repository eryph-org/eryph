using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.GenePool.Model;
using Eryph.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using IdGen;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class UpdateCatletSaga(
        IWorkflow workflow,
        IBus bus,
        IIdGenerator<long> idGenerator,
        IVirtualMachineDataService vmDataService,
        IVirtualMachineMetadataService metadataService)
        : OperationTaskWorkflowSaga<UpdateCatletCommand, UpdateCatletSagaData>(workflow),
            IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
            IHandleMessages<OperationTaskStatusEvent<UpdateCatletVMCommand>>,
            IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>,
            IHandleMessages<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>,
            IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>,
            IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>,
            IHandleMessages<OperationTaskStatusEvent<BreedCatletCommand>>
    {
        protected override void CorrelateMessages(ICorrelationConfig<UpdateCatletSagaData> config)
        {
            base.CorrelateMessages(config);

            config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletVMCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<BreedCatletCommand>>(
                m => m.InitiatingTaskId, d => d.SagaTaskId);
        }

        protected override async Task Initiated(UpdateCatletCommand message)
        {
            Data.BredConfig = message.BreedConfig;
            Data.Config = message.Config;
            Data.CatletId = message.Resource.Id;

            if (Data.CatletId == Guid.Empty)
            {
                await Fail("Catlet cannot be updated because the catlet Id is missing.");
                return;
            }
            
            var machineInfo = await vmDataService.GetVM(Data.CatletId)
                .Map(m => m.IfNoneUnsafe((Catlet?)null));
            if (machineInfo is null)
            {
                await Fail($"Catlet cannot be updated because the catlet {Data.CatletId} does not exist.");
                return;
            }
            
            Data.ProjectId = machineInfo.ProjectId;
            Data.AgentName = machineInfo.AgentName;
            Data.TenantId = machineInfo.Project.TenantId;

            if (Data.ProjectId == Guid.Empty)
            {
                await Fail($"Catlet {Data.CatletId} is not assigned to any project.");
                return;
            }

            if (Data.BredConfig is null)
            {
                await StartNewTask(new ValidateCatletConfigCommand
                {
                    MachineId = message.Resource.Id,
                    Config = message.Config,
                });
                return;
            }

            await PrepareGenes();
        }


        public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
        {
            if (Data.State >= UpdateVMState.ConfigValidated)
                return Task.CompletedTask;

            return FailOrRun(message, async (ValidateCatletConfigCommand r) =>
            {
                Data.State = UpdateVMState.ConfigValidated;
                Data.Config = r.Config;

                await StartNewTask(new BreedCatletCommand()
                {
                    AgentName = Data.AgentName,
                    Config = Data.Config,
                });
            });
        }

        private async Task PrepareGenes()
        {
            if (Data.BredConfig is null)
                throw new InvalidOperationException("Breed config is missing.");

            Data.State = UpdateVMState.ConfigBred;

            var validation = CreatePrepareGeneCommands(Data.BredConfig!);
            if (validation.IsFail)
            {
                await Fail(ErrorUtils.PrintError(Error.New(
                    "Some gene sources are invalid.",
                    Error.Many(validation.FailToSeq()))));
                return;
            }

            var requiredGenes = validation.SuccessToSeq().Flatten()
                .Filter(id => id.GeneIdentifier.GeneName != GeneName.New("catlet"));
            if (requiredGenes.IsEmpty)
            {
                // no images required - go directly to catlet update
                Data.State = UpdateVMState.GenesPrepared;
                Data.PendingGenes = [];
                await UpdateCatlet();
                return;
            }

            Data.SetPendingGenes(requiredGenes);
            var commands = requiredGenes.Map(id => new PrepareGeneCommand
            {
                AgentName = Data.AgentName,
                GeneIdentifier = id,
            });

            foreach (var command in commands)
            {
                await StartNewTask(command);
            }
        }

        internal static Validation<Error, Seq<GeneIdentifierWithType>> CreatePrepareGeneCommands(
            CatletConfig config) =>
            append(
                config.Drives.ToSeq()
                    .Map(c => Optional(c.Source).Filter(s => s.StartsWith("gene:")))
                    .Somes()
                    .Map(s => from geneId in ParseSource(s)
                              select new GeneIdentifierWithType(GeneType.Volume, geneId)),
                config.Fodder.ToSeq()
                    .Map(c => Optional(c.Source).Filter(notEmpty))
                    .Somes()
                    .Map(s => from geneId in ParseSource(s)
                              select new GeneIdentifierWithType(GeneType.Fodder, geneId))
            ).Sequence()
            .Map(s => s.Distinct());


        private static Validation<Error, GeneIdentifier> ParseSource(string source) =>
            GeneIdentifier.NewValidation(source)
                .ToEither()
                .MapLeft(errors => Error.New($"The gene source '{source}' is invalid.", Error.Many(errors)))
                .ToValidation();


        public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
        {
            if (Data.State >= UpdateVMState.GenesPrepared)
                return Task.CompletedTask;

            return FailOrRun(message, (PrepareGeneResponse response) =>
            {
                Data.RemovePendingGene(response.RequestedGene);

                return Data.HasPendingGenes() ? Task.CompletedTask : UpdateCatlet();
            });
        }

        private async Task UpdateCatlet()
        {
            Data.State = UpdateVMState.GenesPrepared;
            await StartNewTask(new UpdateCatletNetworksCommand
            {
                CatletId = Data.CatletId,
                Config = Data.Config,
                ProjectId = Data.ProjectId
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletNetworksCommand> message)
        {
            return FailOrRun(message, async (UpdateCatletNetworksCommandResponse r) =>
            {
                var optionalMachineData = await (
                    from vm in vmDataService.GetVM(Data.CatletId)
                    from metadata in metadataService.GetMetadata(vm.MetadataId)
                    select (vm, metadata));

                await optionalMachineData.Match(
                    Some: data =>
                    {
                        var (vm, metadata) = data;

                        return StartNewTask(new UpdateCatletVMCommand
                        {
                            CatletId = Data.CatletId,
                            VMId = vm.VMId,
                            Config = Data.Config,
                            AgentName = Data.AgentName,
                            NewStorageId = idGenerator.CreateId(),
                            MachineMetadata = metadata,
                            MachineNetworkSettings = r.NetworkSettings
                        }).AsTask();
                    },
                    None: () => Fail(new ErrorData
                    { ErrorMessage = $"Could not find virtual catlet with catlet id {Data.CatletId}" })
                );

            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletVMCommand> message)
        {
            if (Data.State >= UpdateVMState.VMUpdated)
                return Task.CompletedTask;

            return FailOrRun<UpdateCatletVMCommand, ConvergeCatletResult>(message, async r =>
            {
                Data.State = UpdateVMState.VMUpdated;

                await metadataService.SaveMetadata(r.MachineMetadata);

                //TODO: replace this this with operation call
                await bus.SendLocal(new UpdateInventoryCommand
                {
                    AgentName = Data.AgentName,
                    Inventory = new List<VirtualMachineData> { r.Inventory },
                    Timestamp = r.Timestamp,
                });

                // TODO Fix the double await
                await await vmDataService.GetVM(Data.CatletId).Match(
                    Some: data => StartNewTask(new UpdateCatletConfigDriveCommand
                    {
                        VMId = r.Inventory.VMId,
                        CatletId = Data.CatletId,
                        CatletName = data.Name,
                        MachineMetadata = r.MachineMetadata,
                    }).AsTask(),
                    None: () => Fail($"Could not find virtual catlet with catlet id {Data.CatletId}.")
                );
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
        {
            if (Data.State >= UpdateVMState.ConfigDriveUpdated)
                return Task.CompletedTask;

            return FailOrRun(message, async () =>
            {
                Data.State = UpdateVMState.ConfigDriveUpdated;
                
                await StartNewTask(new UpdateNetworksCommand
                {
                    Projects = [Data.ProjectId]
                });
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }

        public Task Handle(OperationTaskStatusEvent<BreedCatletCommand> message)
        {
            if (Data.State >= UpdateVMState.ConfigBred)
                return Task.CompletedTask;

            return FailOrRun(message, (BreedCatletCommandResponse r) =>
            {
                // TODO What about the parent config? There might be
                // a diff between the original parent config (from catlet creation)
                // and the new parent config from the update.
                Data.BredConfig = r.BreedConfig;
                return PrepareGenes();
            });
        }
    }
}
