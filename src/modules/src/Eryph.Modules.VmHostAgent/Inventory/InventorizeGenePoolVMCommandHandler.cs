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
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.Resources.GenePool;
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
        let genePool = genepoolFactory.CreateLocal()
        from genePoolInventory in InventorizeGenePool(vmHostAgentConfig, genePool)
        select new UpdateGenePoolInventoryCommand
        {
            // TODO Use hardware ID instead?
            AgentName = Environment.MachineName,
            Timestamp = timestamp,
            Inventory = genePoolInventory.ToList(),
        };

    private EitherAsync<Error, Seq<GeneSetData>> InventorizeGenePool(
        VmHostAgentConfiguration vmHostAgentConfig,
        ILocalGenePool genePool) =>
        from _ in RightAsync<Error, Unit>(unit)
        let genePoolPath = Path.Combine(vmHostAgentConfig.Defaults.Volumes, "genepool")
        from manifestPaths in Try(() => fileSystemService.GetFiles(
                genePoolPath, "geneset-tag.json", SearchOption.AllDirectories))
            .ToEitherAsync()
        from geneSets in manifestPaths.ToSeq().Map(p => InventorizeGeneSet(Path.GetDirectoryName(p)!, genePool))
            .SequenceSerial()
        select geneSets.Somes();

    private EitherAsync<Error, Option<GeneSetData>> InventorizeGeneSet(
        string geneSetPath,
        ILocalGenePool genePool) =>
        Foo(geneSetPath, genePool)
            .Match(
                Right: Optional,
                Left: error =>
                {
                    logger.LogError(error, "Inventory of gene set {Path} failed", geneSetPath);
                    return None;
                })
            .Map(Right<Error, Option<GeneSetData>>)
            .ToAsync();

    private EitherAsync<Error, GeneSetData> Foo(string geneSetPath, ILocalGenePool genePool) =>
        // TODO filter gene set references
        from geneSetInfo in genePool.GetCachedGeneSet(geneSetPath, default)
        let catletGenes = Optional(geneSetInfo.MetaData.CatletGene)
            .Filter(notEmpty)
            .Map(hash => new GeneData
            {
                GeneType = GeneType.Catlet,
                Id = new GeneIdentifier(geneSetInfo.Id, GeneName.New("catlet")),
                Hash = hash,
                // TODO size
            })
            .ToSeq()
        let fodderGenes = geneSetInfo.MetaData.FodderGenes.ToSeq()
            .Map(grd => new GeneData()
            {
                GeneType = GeneType.Fodder,
                Id = new GeneIdentifier(geneSetInfo.Id, GeneName.New(grd.Name)),
                Hash = grd.Hash,
                // TODO size
            })
        let volumeGenes = geneSetInfo.MetaData.VolumeGenes.ToSeq()
            .Map(grd => new GeneData()
            {
                GeneType = GeneType.Volume,
                Id = new GeneIdentifier(geneSetInfo.Id, GeneName.New(grd.Name)),
                Hash = grd.Hash,
                // TODO size
            })
        select new GeneSetData
        {
            Id = geneSetInfo.Id,
            Genes = catletGenes.Concat(fodderGenes).Concat(volumeGenes).ToList(),
        };
}