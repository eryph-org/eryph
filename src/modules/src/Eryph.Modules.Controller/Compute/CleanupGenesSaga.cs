using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Disks;
using Eryph.Messages.Resources.Genes.Commands;
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
using GeneIdentifierWithType = Eryph.Core.Genetics.GeneIdentifierWithType;

namespace Eryph.Modules.Controller.Compute;

public class CleanupGenesSaga(
    IStateStoreRepository<Gene> geneRepository,
    IStateStoreRepository<VirtualDisk> diskRepository,
    IGeneInventoryQueries geneInventoryQueries,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CleanupGenesCommand, EryphSagaData<CleanupGenesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveGenesVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<CheckDisksExistsCommand>>
{
    protected override async Task Initiated(CleanupGenesCommand message)
    {
        Data.Data.AgentName = Environment.MachineName;

        var unusedGenes = await geneInventoryQueries.FindUnusedGenes(Data.Data.AgentName);
        if (unusedGenes.Count == 0)
        {
            await Complete();
            return;
        }

        var geneIds = unusedGenes.ToSeq()
            .Map(dbGene => dbGene.ToGeneIdWithType())
            .Sequence();
        if (geneIds.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneIds.LeftToSeq())));
            return;
        }

        Data.Data.GeneIds = geneIds.ValueUnsafe().ToList();

        await StartNewTask(new RemoveGenesVMCommand
        {
            AgentName = Data.Data.AgentName,
            Genes = geneIds.ValueUnsafe().ToList(),
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVMCommand> message) =>
        FailOrRun(message, async () =>
        {
            var dbGenes = await geneRepository.ListAsync(
                new GeneSpecs.GetByGeneIds(
                    Data.Data.AgentName,
                    Data.Data.GeneIds.Map(i => i.GeneIdentifier).ToList()));
            await geneRepository.DeleteRangeAsync(dbGenes);

            var volumeGenes = Data.Data.GeneIds
                .Filter(g => g.GeneType == GeneType.Volume)
                .Map(i => i.GeneIdentifier)
                .ToList();
            if (volumeGenes.Count == 0)
            {
                await Complete();
                return;
            }

            var disks = await diskRepository.ListAsync(
                new VirtualDiskSpecs.GetByGeneIds(Data.Data.AgentName, volumeGenes));
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
                    DiskIdentifier = d.DiskIdentifier
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
        config.Correlate<OperationTaskStatusEvent<RemoveGenesVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
