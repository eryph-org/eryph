using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Resources;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class UpdateCatletSaga : OperationTaskWorkflowSaga<UpdateCatletCommand, UpdateCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<ValidateCatletConfigCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<UpdateNetworksCommand>>,
        IHandleMessages<OperationTaskStatusEvent<PrepareGeneCommand>>

    {
        private readonly IBus _bus;
        private readonly Id64Generator _idGenerator;
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IVirtualMachineDataService _vmDataService;


        public UpdateCatletSaga(IWorkflow workflow, IBus bus, Id64Generator idGenerator,
            IVirtualMachineDataService vmDataService,
            IVirtualMachineMetadataService metadataService) : base(workflow)
        {
            _bus = bus;
            _idGenerator = idGenerator;
            _vmDataService = vmDataService;
            _metadataService = metadataService;
        }

        protected override void CorrelateMessages(ICorrelationConfig<UpdateCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<ValidateCatletConfigCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletVMCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<PrepareGeneCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletConfigDriveCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateCatletNetworksCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<UpdateNetworksCommand>>(m => m.InitiatingTaskId,
                d => d.SagaTaskId);

        }

        protected override async Task Initiated(UpdateCatletCommand message)
        {
            Data.Config = message.Config;
            Data.CatletId = message.Resource.Id;
            Data.AgentName = message.AgentName;


            await StartNewTask(new ValidateCatletConfigCommand
                {
                    MachineId = message.Resource.Id,
                    Config = message.Config,
                }
            );
        }


        public Task Handle(OperationTaskStatusEvent<ValidateCatletConfigCommand> message)
        {
            if (Data.Validated)
                return Task.CompletedTask;

            return FailOrRun<ValidateCatletConfigCommand, ValidateCatletConfigCommand>(message, async r =>
            {
                Data.Config = r.Config;
                Data.Validated = true;

                if(Data.CatletId == Guid.Empty)
                    await Fail($"Catlet cannot be updated because the catlet Id is missing.");

                var machineInfo = await _vmDataService.GetVM(Data.CatletId);
                Data.ProjectId = machineInfo.Map(x => x.ProjectId).IfNone(Guid.Empty);
                Data.AgentName = machineInfo.Map(x => x.AgentName).IfNone("");
                Data.TenantId = machineInfo.Map(x => x.Project.TenantId).IfNone(Guid.Empty);


                if (Data.ProjectId == Guid.Empty)
                    await Fail($"Catlet {Data.CatletId} is not assigned to any project.");
                else
                {
                    var breedConfig = await machineInfo.ToAsync().Bind(m =>
                            _metadataService.GetMetadata(m.MetadataId).MapAsync(m => m.ParentConfig ?? new CatletConfig()))
                        .Map(parentConfig => parentConfig?.Breed(Data.Config, Data.Config.Parent) ?? Data.Config)
                        .IfNone(Data.Config);


                    var geneCommands = new List<(GeneType Type, string GeneName)>();
                    geneCommands.AddRange((breedConfig?.Drives?
                        .Select(x => x.Source)
                        .Where(t => t != null && t.StartsWith("gene:") && t.Split(':').Length >= 2)
                        .Select(t => t?.Remove(0,"gene:".Length) ?? "") ?? Enumerable.Empty<string>()).Select(x=> (GeneType.Volume, x)));

                    // no images required - go directly to create
                    if (geneCommands.Count == 0)
                    {
                        await UpdateCatlet();
                        return;
                    }

                    Data.PendingGeneNames = geneCommands.Select(x=>x.GeneName).Distinct().ToList();

                    foreach (var (geneType, geneName) in geneCommands)
                    {
                        await StartNewTask(new PrepareGeneCommand()
                        {
                            GeneType = geneType,
                            GeneName = geneName,
                            AgentName = Data.AgentName
                        });
                    }

                }
            });
        }
        public Task Handle(OperationTaskStatusEvent<PrepareGeneCommand> message)
        {
            if (Data.GenesPrepared)
                return Task.CompletedTask;

            return FailOrRun<PrepareGeneCommand, PrepareGeneResponse>(message,
                (response) =>
                {

                    if (Data.Config != null)
                    {

                        if (response.GeneType == GeneType.Volume)
                        {
                            foreach (var catletDriveConfig in Data.Config.Drives ?? Array.Empty<CatletDriveConfig>())
                            {
                                if (catletDriveConfig.Source != null &&
                                    catletDriveConfig.Source.StartsWith("gene:") &&
                                    catletDriveConfig.Source.Contains(response.RequestedGene))
                                {
                                    catletDriveConfig.Source =
                                        catletDriveConfig.Source.Replace(response.RequestedGene, response.ResolvedGene);
                                }

                            }
                        }
                    }

                    if (Data.PendingGeneNames != null && Data.PendingGeneNames.Contains(response.RequestedGene))
                        Data.PendingGeneNames.Remove(response.ResolvedGene);

                    return Data.PendingGeneNames?.Count == 0
                        ? UpdateCatlet()
                        : Task.CompletedTask;
                });
        }

        private async Task UpdateCatlet()
        {
            Data.GenesPrepared = true;
            await StartNewTask(new UpdateCatletNetworksCommand
            {
                CatletId = Data.CatletId,
                Config = Data.Config,
                ProjectId = Data.ProjectId
            });
        }

        public Task Handle(OperationTaskStatusEvent<UpdateCatletNetworksCommand> message)
        {

            return FailOrRun<UpdateCatletNetworksCommand, UpdateCatletNetworksCommandResponse>(message,
                async r =>
            {

                var optionalMachineData = await (
                    from vm in _vmDataService.GetVM(Data.CatletId)
                    from metadata in _metadataService.GetMetadata(vm.MetadataId)
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
                            NewStorageId = _idGenerator.GenerateId(),
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
            if (Data.Updated)
                return Task.CompletedTask;

            return FailOrRun<UpdateCatletVMCommand, ConvergeCatletResult>(message, async r =>
            {
                Data.Updated = true;

                await _metadataService.SaveMetadata(r.MachineMetadata);

                //TODO: replace this this with operation call
                await _bus.SendLocal(new UpdateInventoryCommand
                {
                    AgentName = Data.AgentName,
                    Inventory = new List<VirtualMachineData> { r.Inventory },
                    TenantId = Data.TenantId
                });

                await await _vmDataService.GetVM(Data.CatletId).Match(
                    Some: data =>
                    {
                        return StartNewTask(new UpdateCatletConfigDriveCommand
                        {
                            VMId = r.Inventory.VMId,
                            CatletId = Data.CatletId,
                            CatletName = data.Name,
                            MachineMetadata = r.MachineMetadata,
                        }).AsTask();
                    },
                    None: () => Fail(new ErrorData
                    { ErrorMessage = $"Could not find virtual catlet with catlet id {Data.CatletId}" })
                );

            });
        }


        public Task Handle(OperationTaskStatusEvent<UpdateCatletConfigDriveCommand> message)
        {
            return FailOrRun(message, () =>
                StartNewTask(new UpdateNetworksCommand
                {
                    Projects = new[] { Data.ProjectId }
                }).AsTask()

                );

        }

        public Task Handle(OperationTaskStatusEvent<UpdateNetworksCommand> message)
        {
            return FailOrRun(message, () => Complete());
        }
    }
}