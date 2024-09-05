using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
using JetBrains.Annotations;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class RemoveGeneSaga(
    IWorkflow workflow,
    IGeneRepository geneRepository)
    : OperationTaskWorkflowSaga<RemoveGeneCommand, EryphSagaData<RemoveGeneSagaData>>(workflow),
        IHandleMessages<OperationTaskStatusEvent<RemoveGenesVMCommand>>
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

        Data.Data.GeneId = dbGene.Id;

        var geneId = dbGene.ToGeneIdWithType();
        if (geneId.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneId.LeftToSeq())));
            return;
        }

        var isUnused = dbGene.GeneType switch
        {
            // Catlet genes are always unused as we store a (modified) copy
            // in the catlet metadata
            GeneType.Catlet => true,
            GeneType.Fodder => await geneRepository.IsUnusedFodderGene(dbGene.Id),
            GeneType.Volume => await geneRepository.IsUnusedVolumeGene(dbGene.Id),
            _ => false,
        };
        if (!isUnused)
        {
            await Fail($"The gene {geneId.ValueUnsafe().GeneIdentifier.Value} is in use.");
            return;
        }

        await StartNewTask(new RemoveGenesVMCommand
        {
            AgentName = dbGene.LastSeenAgent,
            Genes = [geneId.ValueUnsafe()],
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVMCommand> message) =>
        FailOrRun(message, async () =>
        {
            var dbGene = await geneRepository.GetByIdAsync(Data.Data.GeneId);
            if (dbGene is not null)
            {
                await geneRepository.DeleteAsync(dbGene);
            }
            // TODO When deleting a volume gene, we might also need to remove the VirtualDisk entry
            await Complete();
        });

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<RemoveGeneSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<RemoveGenesVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
