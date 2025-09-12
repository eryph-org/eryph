using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.Sys;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

internal interface ILocalGenePool
{
    Aff<CancelRt, Unit> CacheGeneSet(
        GeneSetInfo geneSetInfo);

    Aff<CancelRt, Option<GenesetTagManifestData>> GetCachedGeneSet(
        GeneSetIdentifier geneSetId);

    Aff<CancelRt, Option<string>> GetCachedGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
         GeneHash geneHash);

    Aff<CancelRt, Option<long>> GetGeneSize(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash);

    Aff<CancelRt, Unit> RemoveCachedGene(
        UniqueGeneIdentifier uniqueGeneId);

    Aff<CancelRt, string> CacheGeneContent(
        GeneContentInfo geneContentInfo);

    Aff<CancelRt, HashMap<GenePartHash, Option<long>>> GetDownloadedGeneParts(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        Func<long, long, Task> reportProgress);

    Aff<CancelRt, Unit> MergeGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash, Func<long, long, Task> reportProgress);

    Aff<string> GetTempGenePath(UniqueGeneIdentifier uniqueGeneId, GeneHash geneHash);
}
