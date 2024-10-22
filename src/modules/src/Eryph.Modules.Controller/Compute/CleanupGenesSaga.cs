using System;
using System.Collections.Generic;
using System.Linq;
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
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

public class CleanupGenesSaga(
    IStateStoreRepository<Gene> geneRepository,
    IStateStoreRepository<VirtualDisk> diskRepository,
    IGeneInventoryQueries geneInventoryQueries,
    IStorageManagementAgentLocator agentLocator,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CleanupGenesCommand, EryphSagaData<CleanupGenesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveGenesVmHostCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CheckDisksExistsCommand>>
{
    protected override async Task Initiated(CleanupGenesCommand message)
    {
        Data.Data.AgentName = agentLocator.FindAgentForGenePool();

        var unusedGenes = await geneInventoryQueries.FindUnusedGenes(Data.Data.AgentName);
        if (unusedGenes.Count == 0)
        {
            await Complete();
            return;
        }

        var geneIds = unusedGenes.ToSeq()
            .Map(dbGene => dbGene.ToUniqueGeneId())
            .Sequence();
        if (geneIds.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneIds.LeftToSeq())));
            return;
        }

        Data.Data.GeneIds = geneIds.ValueUnsafe().ToList();

        await StartNewTask(new RemoveGenesVmHostCommand
        {
            AgentName = Data.Data.AgentName,
            Genes = geneIds.ValueUnsafe().ToList(),
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVmHostCommand> message) =>
        FailOrRun(message, async () =>
        {
            var dbGenes = await geneRepository.ListAsync(
                new GeneSpecs.GetByUniqueGeneIds(
                    Data.Data.AgentName,
                    Data.Data.GeneIds));
            await geneRepository.DeleteRangeAsync(dbGenes);

            var volumeGenes = Data.Data.GeneIds
                .Filter(g => g.GeneType == GeneType.Volume)
                .ToList();
            if (volumeGenes.Count == 0)
            {
                await Complete();
                return;
            }

            var disks = await diskRepository.ListAsync(
                new VirtualDiskSpecs.GetByUniqueGeneIds(Data.Data.AgentName, volumeGenes));
            await StartNewTask(new CheckDisksExistsCommand
            {
                AgentName = Data.Data.AgentName,
                Disks = disks.Map(d => new DiskInfo
                {
                    Id = d.Id,
                    ProjectId = d.Project.Id,
                    ProjectName = d.Project.Name,
                    DataStore = d.DataStore,
                    Environment = d.Environment,
                    StorageIdentifier = d.StorageIdentifier,
                    Name = d.Name,
                    FileName = d.FileName,
                    Path = d.Path,
                    DiskIdentifier = d.DiskIdentifier,
                    Gene = d.ToUniqueGeneId(GeneType.Volume)
                        .IfNoneUnsafe((UniqueGeneIdentifier?)null),
                }).ToArray(),
            });
        });

    public Task Handle(OperationTaskStatusEvent<CheckDisksExistsCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CleanupGenesSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<CheckDisksExistsCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<RemoveGenesVmHostCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
