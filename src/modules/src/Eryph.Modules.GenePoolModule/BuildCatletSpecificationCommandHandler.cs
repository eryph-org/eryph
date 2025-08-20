using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Modules.GenePool.Genetics;
using Eryph.Modules.GenePool.Inventory;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

namespace Eryph.Modules.GenePool;

[UsedImplicitly]
internal class BuildCatletSpecificationCommandHandler(
    ITaskMessaging messaging,
    IGenePoolReader genePoolReader,
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genePoolFactory,
    IGenePoolInventoryFactory inventoryFactory)
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
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        let inventory = inventoryFactory.Create(genePoolPath, localGenePool)
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
        from genePoolPath in genePoolPathProvider.GetGenePoolPath()
        let localGenePool = genePoolFactory.CreateLocal(genePoolPath)
        let inventory = inventoryFactory.Create(genePoolPath, localGenePool)
        let geneSetIds = genes.Map(g => g.Id.GeneSet).Distinct()
        from geneData in geneSetIds
            .Map(inventory.InventorizeGeneSet)
            .SequenceSerial()
            .RunWithCancel(CancellationToken.None)
            .ToEitherAsync()
        select geneData.Flatten();
}
