using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

using GeneSetMap = HashMap<GeneSetIdentifier, GeneSetIdentifier>;
using CatletMap = HashMap<GeneSetIdentifier, CatletConfig>;

public static class CatletPedigree
{
    public static Either<Error, (CatletConfig Config, Option<CatletConfig> ParentConfig)> Breed(
        CatletConfig config,
        GeneSetMap geneSetMap,
        CatletMap ancestors) =>
        from parentConfig in BreedRecursively(config.Parent, geneSetMap, ancestors, [])
        from resolvedConfig in CatletGeneResolving.ResolveGenesetIdentifiers(config, geneSetMap)
            .MapLeft(e => Error.New("Could not resolve genes of the catlet.", e))
        from bredConfig in parentConfig.Match(
            Some: pCfg => CatletBreeding.Breed(pCfg, resolvedConfig)
                .MapLeft(e => Error.New("Could not breed the catlet with its parent", e)),
            None: () => resolvedConfig)
        select (bredConfig, parentConfig);

    private static Either<Error, Option<CatletConfig>> BreedRecursively(
        Option<string> id,
        GeneSetMap geneSets,
        CatletMap ancestors,
        Seq<AncestorInfo> visitedAncestors) =>
        from validId in id.Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("The parent ID is invalid.", e))
            .MapLeft(e => CreateError(visitedAncestors, e))
        from bredConfig in validId
            .Map(i => BreedRecursively(i, geneSets, ancestors, visitedAncestors))
            .Sequence()
        select bredConfig;

    private static Either<Error, CatletConfig> BreedRecursively(
        GeneSetIdentifier id,
        GeneSetMap geneSets,
        CatletMap ancestors,
        Seq<AncestorInfo> visitedAncestors) =>
        from resolvedId in geneSets.Find(id)
            .ToEither(Error.New($"Could not resolve the parent ID {id}"))
            .MapLeft(e => CreateError(visitedAncestors, e))
        let updatedVisitedAncestors = visitedAncestors.Add(new AncestorInfo(id, resolvedId))
        from _ in ValidateAncestorChain(updatedVisitedAncestors)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from config in ancestors.Find(resolvedId)
            .ToEither(Error.New($"Could not find the parent config for {resolvedId}"))
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from parentConfig in BreedRecursively(config.Parent, geneSets, ancestors, updatedVisitedAncestors)
        from normalizedConfig in NormalizeGenepoolSources(id, config)
            .MapLeft(e => Error.New("Could not normalize genepool sources.", e))
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from resolvedConfig in CatletGeneResolving.ResolveGenesetIdentifiers(normalizedConfig, geneSets)
            .MapLeft(e => Error.New($"Could not resolve genes in '{id}'."))
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from bredConfig in parentConfig.Match(
            Some: pCfg => CatletBreeding.Breed(pCfg, resolvedConfig)
                .MapLeft(e => Error.New($"Could not breed '{id} with its parent.", e))
                .MapLeft(e => CreateError(updatedVisitedAncestors, e)),
            None: () => resolvedConfig)
        select bredConfig;

    public static Either<Error, Unit> ValidateAncestorChain(
        Seq<AncestorInfo> visitedAncestors) =>
        from _ in guardnot(
                visitedAncestors.Map(p => p.Id).Distinct().Count < visitedAncestors.Count
                || visitedAncestors.Map(p => p.ResolvedId).Distinct().Count < visitedAncestors.Count,
                Error.New("The pedigree contains a circle."))
            .ToEither()
        let maxAncestors = EryphConstants.Limits.MaxCatletAncestors
        from __ in guardnot(visitedAncestors.Count >= maxAncestors,
            Error.New($"The pedigree has too many ancestors (up to {maxAncestors} are allowed)."))
        select unit;

    private static Either<Error, CatletConfig> NormalizeGenepoolSources(
        GeneSetIdentifier id,
        CatletConfig config) =>
        from disks in config.Drives.ToSeq()
            .Map(d => NormalizeGenepoolSources(id, d))
            .Sequence()
        let fodder = config.Fodder.ToSeq()
            .Map(f => NormalizeGenepoolSources(id, f))
        select config.CloneWith(c =>
        {
            c.Drives = disks.ToArray();
            c.Fodder = fodder.ToArray();
        });
    
    private static FodderConfig NormalizeGenepoolSources(
        GeneSetIdentifier id,
        FodderConfig config) =>
        config.CloneWith(c =>
        {
            c.Source = Optional(c.Source).Filter(notEmpty)
                .IfNone(() => new GeneIdentifier(id, GeneName.New("catlet")).Value);
        });

    private static Either<Error, CatletDriveConfig> NormalizeGenepoolSources(
        GeneSetIdentifier id,
        CatletDriveConfig config) =>
        (config.Type ?? CatletDriveType.VHD) == CatletDriveType.VHD && !notEmpty(config.Source)
            ? from geneName in GeneName.NewEither(config.Name)
                .MapLeft(e => Error.New(
                    $"Could not construct volume source for the disk name '{config.Name}'.", e))
                let source = new GeneIdentifier(id, geneName)
              select config.CloneWith(c =>
            {
                c.Source = source.Value;
            })
        : config;


    private static Error CreateError(
        Seq<AncestorInfo> visitedAncestors,
        Error innerError) =>
        Error.New(
            visitedAncestors.Match(
                Empty: () => "Could not breed the catlet config.",
                Seq: ancestors =>
                    "Could not breed ancestor in the pedigree "
                    + string.Join(" -> ", "catlet".Cons(ancestors.Map(a => a.ToString())))
                    + "."),
            innerError);
}
