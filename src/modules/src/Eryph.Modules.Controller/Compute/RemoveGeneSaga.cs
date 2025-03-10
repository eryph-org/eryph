using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Genes.Commands;
using Eryph.Messages.Resources.Disks;
using Eryph.ModuleCore;
using Eryph.Resources.Disks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class RemoveGeneSaga(
    IWorkflow workflow,
    IStateStoreRepository<Gene> geneRepository,
    IStateStoreRepository<VirtualDisk> diskRepository,
    IGeneInventoryQueries geneInventoryQueries)
    : OperationTaskWorkflowSaga<RemoveGeneCommand, EryphSagaData<RemoveGeneSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveGenesVmHostCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CheckDisksExistsCommand>>
{
    protected override async Task Initiated(RemoveGeneCommand message)
    {
        var dbGene = await geneRepository.GetBySpecAsync(
            new GeneSpecs.GetById(message.Id));
        if (dbGene is null)
        {
            await Fail($"The gene {message.Id} was not found.");
            return;
        }

        Data.Data.AgentName = dbGene.LastSeenAgent;

        var geneId = dbGene.ParseUniqueGeneId();
        if (geneId.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneId.LeftToSeq())));
            return;
        }

        Data.Data.GeneId = geneId.ValueUnsafe();

        var isUnused = await geneInventoryQueries.IsUnusedGene(dbGene.Id);
        if (!isUnused)
        {
            await Fail($"The gene {geneId.ValueUnsafe()} is in use.");
            return;
        }

        await StartNewTask(new RemoveGenesVmHostCommand
        {
            AgentName = dbGene.LastSeenAgent,
            Genes = [Data.Data.GeneId],
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVmHostCommand> message) =>
        FailOrRun(message, async () =>
        {
            var dbGene = await geneRepository.GetBySpecAsync(
                new GeneSpecs.GetByUniqueGeneId(Data.Data.AgentName, Data.Data.GeneId));
            if (dbGene is not null)
            {
                await geneRepository.DeleteAsync(dbGene);
            }

            if (Data.Data.GeneId.GeneType != GeneType.Volume)
            {
                await Complete();
                return;
            }

            var disk = await diskRepository.GetBySpecAsync(
                new VirtualDiskSpecs.GetByUniqueGeneId(
                    Data.Data.AgentName,
                    Data.Data.GeneId));
            if (disk is null)
            {
                await Complete();
                return;
            }

            await StartNewTask(new CheckDisksExistsCommand
            {
                AgentName = Data.Data.AgentName,
                Disks =
                [
                    new DiskInfo
                    {
                        Id = disk.Id,
                        ProjectId = disk.Project.Id,
                        ProjectName = disk.Project.Name,
                        DataStore = disk.DataStore,
                        Environment = disk.Environment,
                        StorageIdentifier = disk.StorageIdentifier,
                        Name = disk.Name,
                        FileName = disk.FileName,
                        Path = disk.Path,
                        DiskIdentifier = disk.DiskIdentifier,
                        Gene = disk.ToUniqueGeneId(GeneType.Volume)
                            .IfNoneUnsafe((UniqueGeneIdentifier?)null),
                    }
                ]
            });
        });

    public Task Handle(OperationTaskStatusEvent<CheckDisksExistsCommand> message) =>
        FailOrRun(message, Complete);

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<RemoveGeneSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<CheckDisksExistsCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<RemoveGenesVmHostCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
