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
    public static Either<Error, CatletConfig> Breed(
        CatletConfig config,
        GeneSetMap geneSetMap,
        CatletMap ancestors) =>
        from parentConfig in BreedRecursively(config.Parent, geneSetMap, ancestors, [])
        from resolvedConfig in CatletGeneResolving.ResolveGeneSetIdentifiers(config, geneSetMap)
            .MapLeft(e => Error.New("Could not resolve genes of the catlet.", e))
        from bredConfig in parentConfig.Match(
            Some: pCfg => CatletBreeding.Breed(pCfg, resolvedConfig)
                .MapLeft(e => Error.New("Could not breed the catlet with its parent.", e)),
            None: () => resolvedConfig)
        let resultConfig = Mutate(bredConfig)
        select resultConfig;

    private static Either<Error, Option<CatletConfig>> BreedRecursively(
        Option<string> id,
        GeneSetMap geneSets,
        CatletMap ancestors,
        Seq<AncestorInfo> visitedAncestors) =>
        from validId in id.Filter(notEmpty)
            .Map(vId => GeneSetIdentifier.NewEither(vId)
                .MapLeft(e => Error.New($"The parent source '{vId}' is invalid.", e)))
            .Sequence()
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
            .ToEither(Error.New($"Could not resolve the parent source '{id}'."))
            .MapLeft(e => CreateError(visitedAncestors, e))
        let updatedVisitedAncestors = visitedAncestors.Add(new AncestorInfo(id, resolvedId))
        from _ in ValidateAncestorChain(updatedVisitedAncestors)
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from config in ancestors.Find(resolvedId)
            .ToEither(Error.New($"Could not find the config for '{resolvedId}'."))
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from parentConfig in BreedRecursively(config.Parent, geneSets, ancestors, updatedVisitedAncestors)
        from resolvedConfig in CatletGeneResolving.ResolveGeneSetIdentifiers(config, geneSets)
            .MapLeft(e => Error.New($"Could not resolve genes in catlet '{resolvedId}'.", e))
            .MapLeft(e => CreateError(updatedVisitedAncestors, e))
        from bredConfig in parentConfig.Match(
            Some: pCfg => CatletBreeding.Breed(pCfg, resolvedConfig)
                .MapLeft(e => Error.New($"Could not breed catlet '{resolvedId}' with its parent.", e))
                .MapLeft(e => CreateError(updatedVisitedAncestors, e)),
            None: () => resolvedConfig)
        select bredConfig;

    private static CatletConfig Mutate(CatletConfig config) =>
        config.CloneWith(c =>
        {
            c.Capabilities = c.Capabilities.ToSeq()
                .Filter(cap => cap.Mutation != MutationType.Remove)
                .ToArray();
            c.Drives = c.Drives.ToSeq()
                .Filter(d => d.Mutation != MutationType.Remove)
                .ToArray();
            c.Networks = c.Networks.ToSeq()
                .Filter(n => n.Mutation != MutationType.Remove)
                .ToArray();
            c.NetworkAdapters = c.NetworkAdapters.ToSeq()
                .Filter(n => n.Mutation != MutationType.Remove)
                .ToArray();
            c.Fodder = c.Fodder.ToSeq()
                .Filter(f => f.Remove != true)
                .ToArray();
        });

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
