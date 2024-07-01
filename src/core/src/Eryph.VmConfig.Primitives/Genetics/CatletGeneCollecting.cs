using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static LanguageExt.Seq;


namespace Eryph.Genetics;

public static class CatletGeneCollecting
{
    public static Validation<Error, Seq<GeneIdentifierWithType>> CollectGenes(
        CatletConfig catletConfig) =>
        append(Optional(catletConfig.Parent).ToSeq().Map(CollectParent),
                catletConfig.Drives.ToSeq().Map(CollectGenes),
                catletConfig.Fodder.ToSeq().Map(CollectGenes))
            .Sequence()
            .Map(l => l.Somes().Distinct());

    private static Validation<Error, Option<GeneIdentifierWithType>> CollectParent(
        string parentId) =>
        Optional(parentId)
            .Filter(notEmpty)
            .Map(id => GeneSetIdentifier.NewValidation(id)
                .ToEither()
                .MapLeft(errors => Error.New($"The parent source '{parentId}' is invalid.", Error.Many(errors)))
                .ToValidation())
            .MapT(geneSetId => new GeneIdentifierWithType(
                GeneType.Catlet,
                new GeneIdentifier(geneSetId, GeneName.New("catlet"))))
            .Sequence();

    private static Validation<Error, Option<GeneIdentifierWithType>> CollectGenes(
        CatletDriveConfig driveConfig) =>
        Optional(driveConfig.Source)
            .Filter(notEmpty)
            .Filter(s => s.StartsWith("gene:"))
            .Map(ParseSource)
            .MapT(geneId => new GeneIdentifierWithType(GeneType.Volume, geneId))
            .Sequence();

    private static Validation<Error, Option<GeneIdentifierWithType>> CollectGenes(
        FodderConfig fodderConfig) =>
        Optional(fodderConfig.Source)
            .Filter(notEmpty)
            .Map(ParseSource)
            .MapT(geneId => new GeneIdentifierWithType(GeneType.Fodder, geneId))
            .Sequence();

    private static Validation<Error, GeneIdentifier> ParseSource(string source) =>
        GeneIdentifier.NewValidation(source)
            .ToEither()
            .MapLeft(errors => Error.New($"The gene source '{source}' is invalid.", Error.Many(errors)))
            .ToValidation();
}