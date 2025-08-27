using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.GenePool.Inventory;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
internal class BuildCatletSpecificationCommandHandler(
    ITaskMessaging messaging,
    IGenePoolReader genePoolReader,
    IGenePoolInventory genePoolInventory)
    : IHandleMessages<OperationTask<BuildCatletSpecificationGenePoolCommand>>
{
    public Task Handle(OperationTask<BuildCatletSpecificationGenePoolCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, BuildCatletSpecificationGenePoolCommandResponse> HandleCommand(
        BuildCatletSpecificationGenePoolCommand genePoolCommand) =>
        from result in CatletSpecificationBuilder.Build(
            genePoolCommand.CatletConfig,
            genePoolCommand.CatletArchitecture,
            genePoolReader,
            CancellationToken.None)
        let timestamp = DateTimeOffset.UtcNow
        from geneData in InventorizeGenes(result.ResolvedGenes.Keys.ToSeq())
        select new BuildCatletSpecificationGenePoolCommandResponse
        {
            BuiltConfig = result.ExpandedConfig,
            ResolvedGenes = result.ResolvedGenes.ToDictionary(),
            Inventory = geneData.ToList(),
            Timestamp = timestamp,
        };

    private EitherAsync<Error, Seq<GeneData>> InventorizeGenes(
        Seq<UniqueGeneIdentifier> genes) =>
        from _ in RightAsync<Error, Unit>(unit)
        let geneSetIds = genes.Map(g => g.Id.GeneSet).Distinct()
        from geneData in geneSetIds
            .Map(genePoolInventory.InventorizeGeneSet)
            .SequenceSerial()
            .RunWithCancel(CancellationToken.None)
            .ToEitherAsync()
        select geneData.Flatten();
}
