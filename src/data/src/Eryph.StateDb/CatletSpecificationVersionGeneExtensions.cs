using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;

namespace Eryph.StateDb;

public static class CatletSpecificationVersionGeneExtensions
{
    public static IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ToGenesDictionary(
        this IList<CatletSpecificationVersionGene> genes) =>
        genes.ToDictionary(
            g => new UniqueGeneIdentifier(
                g.GeneType,
                new GeneIdentifier(GeneSetIdentifier.New(g.GeneSet), GeneName.New(g.Name)),
                Architecture.New(g.Architecture)),
            g => GeneHash.New(g.Hash));
    

    public static IList<CatletSpecificationVersionGene> ToGenesList(
        this IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> genes,
        Guid specificationVersionId) =>
        genes.Map(kvp => new CatletSpecificationVersionGene
        {
            SpecificationVersionId = specificationVersionId,
            GeneType = kvp.Key.GeneType,
            Architecture = kvp.Key.Architecture.Value,
            GeneSet = kvp.Key.Id.GeneSet.Value,
            Name = kvp.Key.Id.GeneName.Value,
            Hash = kvp.Value.Value,
        }).ToList();
}
