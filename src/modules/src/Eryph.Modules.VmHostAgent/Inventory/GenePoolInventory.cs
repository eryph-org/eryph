using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool;
using Eryph.Modules.VmHostAgent.Genetics;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

internal class GenePoolInventory(
    ILogger logger,
    IFileSystemService fileSystemService,
    string genePoolPath,
    ILocalGenePool genePool)
    : IGenePoolInventory
{
    public EitherAsync<Error, Seq<GeneData>> InventorizeGenePool() =>
        from _ in RightAsync<Error, Unit>(unit)
        from manifestPaths in Try(() => fileSystemService.GetFiles(
                genePoolPath, "geneset-tag.json", SearchOption.AllDirectories))
            .ToEitherAsync()
        from geneSets in manifestPaths.ToSeq()
            .Map(p => InventorizeGeneSet(p)
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

    public EitherAsync<Error, Seq<GeneData>> InventorizeGeneSet(
        GeneSetIdentifier geneSetId) =>
        from geneSetInfo in genePool.GetCachedGeneSet(genePoolPath, geneSetId, default)
        from geneSetData in notEmpty(geneSetInfo.MetaData.Reference)
            ? RightAsync<Error, Seq<GeneData>>(Seq<GeneData>())
            : InventorizeGeneSet(geneSetInfo)
        select geneSetData;

    private EitherAsync<Error, Seq<GeneData>> InventorizeGeneSet(
        string geneSetManifestPath) =>
        from geneSetId in GenePoolPaths.GetGeneSetIdFromManifestPath(genePoolPath, geneSetManifestPath)
            .ToAsync()
        from geneData in InventorizeGeneSet(geneSetId)
        select geneData;

    private EitherAsync<Error, Seq<GeneData>> InventorizeGeneSet(
        GeneSetInfo geneSetInfo) =>
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
            .Map(g => InventorizeGene(geneSetInfo.Id, g.GeneType, g.Name, g.Hash))
            .SequenceSerial()
        select geneData.Somes();

    private EitherAsync<Error, Option<GeneData>> InventorizeGene(
        GeneSetIdentifier geneSetId,
        GeneType geneType,
        string geneName,
        string hash) =>
        from validGeneName in GeneName.NewEither(geneName).ToAsync()
        let geneId = new GeneIdentifier(geneSetId, validGeneName)
        let genePath = GenePoolPaths.GetGenePath(genePoolPath, geneType, geneId)
        from size in genePool.GetCachedGeneSize(genePoolPath, geneType, geneId)
        select size.Map(s => new GeneData
        {
            GeneType = geneType,
            Id = geneId,
            Hash = hash,
            Size = s,
        });
}