using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.GenePool;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
internal class InventorizeGenePoolCommandHandler(
    IBus bus,
    ILogger logger,
    IGenePoolFactory genepoolFactory,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IFileSystemService fileSystemService,
    WorkflowOptions workflowOptions)
    : IHandleMessages<InventorizeGenePoolCommand>
{
    public Task Handle(InventorizeGenePoolCommand message) =>
        InventorizeGenePool().MatchAsync(
            RightAsync: c => bus.Advanced.Routing.Send(workflowOptions.OperationsDestination, c),
            LeftAsync: error =>
            {
                logger.LogError(error, "Inventory of gene pool on host {HostName} failed", Environment.MachineName);
                        return Task.CompletedTask;
            });

    private EitherAsync<Error, UpdateGenePoolInventoryCommand> InventorizeGenePool() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let timestamp = DateTimeOffset.UtcNow
        let genePoolPath = GenePoolPaths.GetGenePoolPath(vmHostAgentConfig)
        let genePool = genepoolFactory.CreateLocal()
        from genePoolInventory in InventorizeGenePool(genePoolPath, genePool)
        select new UpdateGenePoolInventoryCommand
        {
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = genePoolInventory.ToList(),
        };

    private EitherAsync<Error, Seq<GeneData>> InventorizeGenePool(
        string genePoolPath,
        ILocalGenePool genePool) =>
        from _ in RightAsync<Error, Unit>(unit)
        from manifestPaths in Try(() => fileSystemService.GetFiles(
                genePoolPath, "geneset-tag.json", SearchOption.AllDirectories))
            .ToEitherAsync()
        from geneSets in manifestPaths.ToSeq()
            .Map(p => InventorizeGeneSet(p, genePoolPath, genePool)
                .Match(Right: identity,
                    Left: error =>
                    {
                        logger.LogError(error, "Inventory of gene set {Path} failed", p);
                        return Seq<GeneData>();
                    }))
            .SequenceSerial()
            .Map(s => s.Flatten())
            .Map(Right<Error, Seq<GeneData>>)
            .ToAsync()
        select geneSets;

    private EitherAsync<Error, Seq<GeneData>> InventorizeGeneSet(
        string geneSetManifestPath,
        string genePoolPath,
        ILocalGenePool genePool) =>
        from geneSetId in GenePoolPaths.GetGeneSetIdFromPath(genePoolPath, geneSetManifestPath)
            .ToAsync()
        from geneSetInfo in genePool.GetCachedGeneSet(genePoolPath, geneSetId, default)
        from _ in guard(geneSetInfo.Id == geneSetId,
            Error.New($"The gene set manifest '{geneSetManifestPath}' is in the wrong location."))
        from geneSetData in notEmpty(geneSetInfo.MetaData.Reference)
            ? RightAsync<Error, Seq<GeneData>>(Seq<GeneData>())
            : InventorizeGeneSet(geneSetInfo, genePoolPath)
        select geneSetData;

    private EitherAsync<Error, Seq<GeneData>> InventorizeGeneSet(
        GeneSetInfo geneSetInfo,
        string genePoolPath) =>
        from _ in RightAsync<Error, Unit>(unit)
        let catletGenes = Optional(geneSetInfo.MetaData.CatletGene)
            .Filter(notEmpty)
            .Map(hash => (GeneType: GeneType.Catlet, Name: "catlet", Hash: hash))
            .ToSeq()
        let fodderGenes = geneSetInfo.MetaData.FodderGenes.ToSeq()
            .Map(grd => (GeneType: GeneType.Fodder, grd.Name, grd.Hash))
        let volumeGenes = geneSetInfo.MetaData.VolumeGenes.ToSeq()
            .Map(grd => (GeneType: GeneType.Volume, grd.Name, grd.Hash))
        let allGenes = catletGenes.Append(fodderGenes).Append(volumeGenes)
        from geneData in allGenes
            .Map(g => InventorizeGene(genePoolPath, geneSetInfo.Id, g.GeneType, g.Name, g.Hash))
            .Sequence()
            .ToAsync()
        select geneData.Somes();

    private Either<Error, Option<GeneData>> InventorizeGene(
        string genePoolPath,
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        string geneName,
        string hash) =>
        from validGeneName in GeneName.NewEither(geneName)
        let geneId = new GeneIdentifier(geneSetId, validGeneName)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, geneId)
        from size in Try(() => fileSystemService.FileExists(genePath)
                ? Some(fileSystemService.GetFileSize(genePath)) : None)
            .ToEither().MapLeft(Error.New)
        select size.Map(s => new GeneData
        {
            GeneType = geneType,
            Id = geneId,
            Hash = hash,
            Size = s,
        });
}
