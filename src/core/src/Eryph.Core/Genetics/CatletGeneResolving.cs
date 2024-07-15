using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;

public static class CatletGeneResolving
{
    public static Either<Error, CatletConfig> ResolveGeneSetIdentifiers(
    CatletConfig catletConfig,
    GeneSetMap resolvedGeneSets) =>
        from resolvedParent in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(p => GeneSetIdentifier.NewEither(p)
                .MapLeft(e => Error.New($"The parent source '{catletConfig.Parent}' is invalid.", e)))
            .BindT(geneSetId => ResolveGeneSetIdentifier(geneSetId, resolvedGeneSets))
            .Sequence()
        from resolvedDrives in catletConfig.Drives.ToSeq()
            .Map(driveConfig => ResolveGeneSetIdentifiers(driveConfig, resolvedGeneSets))
            .Sequence()
        from resolvedFodder in catletConfig.Fodder.ToSeq()
            .Map(fodderConfig => ResolveGeneSetIdentifiers(fodderConfig, resolvedGeneSets))
            .Sequence()
        select catletConfig.CloneWith(c =>
        {
            c.Parent = resolvedParent.Map(id => id.Value).IfNoneUnsafe((string)null);
            c.Drives = resolvedDrives.ToArray();
            c.Fodder = resolvedFodder.ToArray();
        });

    private static Either<Error, FodderConfig> ResolveGeneSetIdentifiers(
        FodderConfig fodderConfig,
        GeneSetMap resolvedGeneSets) =>
        from resolvedGeneIdentifier in ResolveGeneSetIdentifier(fodderConfig.Source, resolvedGeneSets)
        select fodderConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Map(id => id.Value)
                .IfNoneUnsafe((string)null);
        });

    private static Either<Error, CatletDriveConfig> ResolveGeneSetIdentifiers(
        CatletDriveConfig driveConfig,
        GeneSetMap resolvedGeneSets) =>
        from resolvedGeneIdentifier in ResolveGeneSetIdentifier(driveConfig.Source, resolvedGeneSets)
        select driveConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Map(id => id.Value)
                .IfNoneUnsafe((string)null);
        });

    private static Either<Error, Option<GeneIdentifier>> ResolveGeneSetIdentifier(
        Option<string> geneIdentifier,
        GeneSetMap resolvedGeneSets) =>
        from validGeneId in geneIdentifier
            .Filter(notEmpty)
            .Map(GeneIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New($"The gene ID '{geneIdentifier}' is invalid.", e))
        from resolvedGeneSetId in validGeneId
            .Map(geneId => geneId.GeneSet)
            .Map(geneSetId => ResolveGeneSetIdentifier(geneSetId, resolvedGeneSets))
            .Sequence()
        select from geneSetId in resolvedGeneSetId
               from geneId in validGeneId
               select new GeneIdentifier(geneSetId, geneId.GeneName);

    private static Either<Error, GeneSetIdentifier> ResolveGeneSetIdentifier(
        GeneSetIdentifier geneSetId,
        GeneSetMap resolvedGeneSets) =>
        resolvedGeneSets.Find(geneSetId)
            .ToEither(Error.New($"The gene set '{geneSetId}' could not be resolved."));
}
