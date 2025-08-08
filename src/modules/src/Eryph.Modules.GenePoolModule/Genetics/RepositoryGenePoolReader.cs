using Eryph.ConfigModel;
using Eryph.Configuration.Model;
using Eryph.GenePool.Client;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

internal class RepositoryGenePoolReader(
    IGenePoolFactory genePoolFactory,
    ILogger logger)
    : IRepositoryGenePoolReader
{
    public EitherAsync<Error, Option<GeneInfo>> ProvideGene(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let _2 = fun(() => logger.LogDebug("Trying to find gene {GeneId} ({GeneHash}) on remote pools", uniqueGeneId, geneHash))
        from geneInfos in genePoolFactory.RemotePools
            .Map(genePoolName => ProvideGene(genePoolName, uniqueGeneId, geneHash, cancellationToken))
            .SequenceSerial()
        let geneInfo = geneInfos.Somes().HeadOrNone()
        select geneInfo;

    private EitherAsync<Error, Option<GeneInfo>> ProvideGene(
        string genePoolName,
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from genePool in Try(() => genePoolFactory.CreateNew(genePoolName))
            .ToEitherAsync()
            .MapLeft(_ => Error.New($"Could not find the gene pool '{genePoolName}'."))
        from response in genePool.RetrieveGene(uniqueGeneId, geneHash, cancellationToken)
            .BiBind(
                Right: gi =>
                {
                    logger.LogDebug("Found gene {GeneId} ({GeneHash}) on gene pool '{GenePoolName}'", uniqueGeneId, geneHash, genePoolName);
                    return RightAsync<Error, Option<GeneInfo>>(Some(gi));
                },
                Left: error =>
                {
                    logger.LogInformation(error, "Failed to lookup gene {GeneId} ({GeneHash}) on gene pool '{GenePool}'", uniqueGeneId, geneHash, genePoolName);
                    return IsUnexpectedHttpClientError(error)
                        ? LeftAsync<Error, Option<GeneInfo>>(error)
                        : RightAsync<Error, Option<GeneInfo>>(Option<GeneInfo>.None);
                })
        select response;

    public EitherAsync<Error, Option<GeneContentInfo>> ProvideGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let _2 = fun(() => logger.LogDebug("Trying to find gene {GeneId} ({GeneHash}) on remote pools", uniqueGeneId, geneHash))
        from geneContentInfos in genePoolFactory.RemotePools
            .Map(genePoolName => ProvideGeneContent(genePoolName, uniqueGeneId, geneHash, cancellationToken))
            .SequenceSerial()
        let geneContentInfo = geneContentInfos.Somes().HeadOrNone()
        select geneContentInfo;

    private EitherAsync<Error, Option<GeneContentInfo>> ProvideGeneContent(
        string genePoolName,
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken) =>
        from genePool in Try(() => genePoolFactory.CreateNew(genePoolName))
            .ToEitherAsync()
            .MapLeft(_ => Error.New($"Could not find the gene pool '{genePoolName}'."))
        from response in genePool.RetrieveGeneContent(uniqueGeneId, geneHash, cancellationToken)
            .BiBind(
                Right: gci =>
                {
                    logger.LogDebug("Found gene {GeneId} ({GeneHash}) on gene pool '{GenePoolName}'", uniqueGeneId, geneHash, genePoolName);
                    return RightAsync<Error, Option<GeneContentInfo>>(Some(gci));
                },
                Left: error =>
                {
                    logger.LogInformation(error, "Failed to lookup gene {GeneId} ({GeneHash}) on gene pool '{GenePool}'", uniqueGeneId, geneHash, genePoolName);
                    return IsUnexpectedHttpClientError(error)
                        ? LeftAsync<Error, Option<GeneContentInfo>>(error)
                        : RightAsync<Error, Option<GeneContentInfo>>(Option<GeneContentInfo>.None);
                })
        select response;

    public EitherAsync<Error, Option<GeneSetInfo>> ProvideGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let _2 = fun(() => logger.LogDebug("Trying to find geneset {GeneSetId} on remote pools", geneSetId))
        from geneSetInfos in genePoolFactory.RemotePools
            .Map(genePoolName => ProvideGeneSet(genePoolName, geneSetId, cancellationToken))
            .SequenceSerial()
        let geneSetInfo = geneSetInfos.Somes().HeadOrNone()
        select geneSetInfo;

    private EitherAsync<Error, Option<GeneSetInfo>> ProvideGeneSet(
        string genePoolName,
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken) =>
        from genePool in Try(() => genePoolFactory.CreateNew(genePoolName))
            .ToEitherAsync()
            .MapLeft(_ => Error.New($"Could not find the gene pool '{genePoolName}'."))
        from response in genePool.ProvideGeneSet(geneSetId, cancellationToken)
            .BiBind(
                Right: gsi =>
                {
                    logger.LogDebug("Found geneset {GeneSetId} on gene pool '{GenePoolName}'", geneSetId, genePoolName);
                    return RightAsync<Error, Option<GeneSetInfo>>(Some(gsi));
                },
                Left: error =>
                {
                    logger.LogInformation(error, "Failed to lookup geneset {GenesetId} on gene pool '{GenePool}'", geneSetId, genePoolName);
                    return IsUnexpectedHttpClientError(error)
                        ? LeftAsync<Error, Option<GeneSetInfo>>(error)
                        : RightAsync<Error, Option<GeneSetInfo>>(Option<GeneSetInfo>.None);
                })
        select response;

    private static bool IsUnexpectedHttpClientError(Error error) =>
        error.Exception
            .Map(ex => ex is GenepoolClientException
            {
                StatusCode: >= HttpStatusCode.BadRequest
                and < HttpStatusCode.InternalServerError
                and not HttpStatusCode.NotFound
            })
            .IfNone(false);
}
