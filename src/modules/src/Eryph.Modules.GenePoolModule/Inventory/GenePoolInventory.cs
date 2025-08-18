using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.GenePool;
using Eryph.GenePool.Model;
using Eryph.Modules.GenePool.Genetics;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Inventory;

internal class GenePoolInventory(
    ILogger logger,
    IFileSystemService fileSystemService,
    string genePoolPath,
    ILocalGenePool genePool)
    : IGenePoolInventory
{
    public Aff<CancelRt, Seq<GeneData>> InventorizeGenePool() =>
        from manifestPaths in Eff(() => fileSystemService.GetFiles(
                genePoolPath, "geneset-tag.json", SearchOption.AllDirectories))
        from geneSets in manifestPaths.ToSeq()
            .Map(p => InventorizeGeneSet(p).Match(
                Succ: identity,
                Fail: error =>
                {
                    logger.LogError(error, "Inventory of gene set manifest {Path} failed", p);
                    return Seq<GeneData>();
                }))
            .SequenceSerial()
            .Map(s => s.Flatten())
        select geneSets;

    public Aff<CancelRt, Seq<GeneData>> InventorizeGeneSet(
        GeneSetIdentifier geneSetId) =>
        from optionalManifest in genePool.GetCachedGeneSet(geneSetId)
        from manifest in optionalManifest
            .ToAff(Error.New($"Could not find the manifest for gene set {geneSetId}."))
        from geneSetData in notEmpty(manifest.Reference)
            ? SuccessAff<CancelRt, Seq<GeneData>>(Empty)
            : InventorizeGeneSet(geneSetId, manifest)
        select geneSetData;

    private Aff<CancelRt, Seq<GeneData>> InventorizeGeneSet(
        string geneSetManifestPath) =>
        from geneSetId in GenePoolPaths.GetGeneSetIdFromManifestPath(genePoolPath, geneSetManifestPath)
            .ToAff()
        from geneData in InventorizeGeneSet(geneSetId)
        select geneData;

    private Aff<CancelRt, Seq<GeneData>> InventorizeGeneSet(
        GeneSetIdentifier geneSetId,
        GenesetTagManifestData manifest) =>
        from _ in SuccessAff(unit)
        from genes in GeneSetTagManifestUtils.GetGenes(manifest)
            .ToAff(e => Error.New($"The manifest of gene set {geneSetId} contains invalid genes.", e))
        from geneData in genes.ToSeq()
            .Map(kvp => InventorizeGene(kvp.Key, kvp.Value))
            .SequenceSerial()
        select geneData.Somes();

    private Aff<CancelRt, Option<GeneData>> InventorizeGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash) =>
        from size in genePool.GetCachedGeneSize2(uniqueGeneId)
        select size.Map(s => new GeneData
        {
            Id = uniqueGeneId,
            Hash = geneHash,
            Size = s,
        });
}
