using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.StateDb;

public static class StateStoreGeneExtensions
{
    public static Either<Error, UniqueGeneIdentifier> ParseUniqueGeneId(
        this Gene dbGene) =>
        from geneSetId in GeneSetIdentifier.NewEither(dbGene.GeneSet)
        from geneName in GeneName.NewEither(dbGene.Name)
        from architecture in Architecture.NewEither(dbGene.Architecture)
        let geneId = new GeneIdentifier(geneSetId, geneName)
        select new UniqueGeneIdentifier(dbGene.GeneType, geneId, architecture);

    public static Either<Error, Option<UniqueGeneIdentifier>> ParseUniqueGeneId(
        this VirtualDisk disk,
        GeneType geneType) =>
        from geneSetId in Optional(disk.GeneSet)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
        from geneName in Optional(disk.GeneName)
            .Map(GeneName.NewEither)
            .Sequence()
        from architecture in Optional(disk.GeneArchitecture)
            .Map(Architecture.NewEither)
            .Sequence()
        select from validGeneSetId in geneSetId
            from validGeneName in geneName
            from validArchitecture in architecture
            let geneId = new GeneIdentifier(validGeneSetId, validGeneName)
            select new UniqueGeneIdentifier(geneType, geneId, validArchitecture);

    public static UniqueGeneIdentifier ToUniqueGeneId(this Gene dbGene) =>
        dbGene.ParseUniqueGeneId()
            .IfLeft(e => e.ToException().Rethrow<UniqueGeneIdentifier>());

    public static Option<UniqueGeneIdentifier> ToUniqueGeneId(
        this VirtualDisk disk,
        GeneType geneType) =>
        disk.ParseUniqueGeneId(geneType)
            .IfLeft(e => e.ToException().Rethrow<UniqueGeneIdentifier>());

    public static string ToUniqueGeneIndex(this UniqueGeneIdentifier geneId) =>
        $"{geneId.Id.GeneSet}|{geneId.Id.GeneName}|{geneId.Architecture}";

    internal static string? ToUniqueGeneIndex(string? geneSet, string? name, string? architecture) =>
        geneSet is not null && name is not null && architecture is not null
            ? $"{geneSet}|{name}|{architecture}"
            : null;
}
