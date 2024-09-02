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
    IStateStoreRepository<Gene> geneRepository)
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

        // TODO check that the gene is not used
        // For volumes check virtual disks
        // For fodder check catlet metadata
        var geneId = dbGene.ToGeneIdWithType();
        if (geneId.IsLeft)
        {
            await Fail(ErrorUtils.PrintError(Error.Many(geneId.LeftToSeq())));
            return;
        }

        await StartNewTask(new RemoveGenesVMCommand
        {
            AgentName = dbGene.LastSeenAgent,
            Genes = [geneId.ValueUnsafe()],
        });
    }

    public Task Handle(OperationTaskStatusEvent<RemoveGenesVMCommand> message) =>
        FailOrRun(message, () => Complete());

    protected override void CorrelateMessages(ICorrelationConfig<EryphSagaData<RemoveGeneSagaData>> config)
    {
        base.CorrelateMessages(config);

        config.Correlate<OperationTaskStatusEvent<RemoveGenesVMCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }
}
