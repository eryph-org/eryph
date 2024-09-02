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
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.ModuleCore;
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
    StateStoreContext dbContext,
    IStateStoreRepository<Gene> geneRepository,
    IWorkflow workflow)
    : OperationTaskWorkflowSaga<CleanupGenesCommand, EryphSagaData<CleanupGenesSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveGenesVMCommand>>
{
    protected override async Task Initiated(CleanupGenesCommand message)
    {
        var agentName = Environment.MachineName;

        var unusedGenes = await geneRepository.ListAsync(
            new GeneSpecs.GetUnused(agentName, dbContext));
        if (unusedGenes.Count == 0)
        {
            await Complete();
            return;
        }

        var geneIds = unusedGenes.ToSeq()
            .Map(dbGene => from geneSetId in GeneSetIdentifier.NewEither(dbGene.GeneSet)
                from geneName in GeneName.NewEither(dbGene.Name)
                let geneId = new GeneIdentifier(geneSetId, geneName)
                select new GeneIdentifierWithType(dbGene.GeneType, geneId))
            .Sequence();
        if (geneIds.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneIds.LeftToSeq())));
            return;
        }


        await StartNewTask(new RemoveGenesVMCommand
        {
            AgentName = agentName,
            Genes = geneIds.ValueUnsafe().ToList(),
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<CleanupGenesSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<RemoveGenesVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
