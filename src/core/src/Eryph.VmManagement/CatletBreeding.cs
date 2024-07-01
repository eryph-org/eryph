using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.GenePool.Model;
using Eryph.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;
using static LanguageExt.Seq;

namespace Eryph.VmManagement;

public static class CatletBreeding
{
    public static Validation<Error, Seq<GeneSetIdentifier>> CollectGeneSetsRecursively(
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        from optionalParent in Optional(catletConfig.Parent)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewValidation)
            .Sequence()
        from parentGeneSets in optionalParent.Map(parentId =>
                from resolvedParentId in ResolveGeneSetIdentifier(parentId, genepoolReader)
                    .ToValidation()
                from parentConfig in ReadCatletConfig(resolvedParentId, genepoolReader)
                    .ToValidation()
                from parentGeneSets in CollectGeneSetsRecursively(parentConfig, genepoolReader)
                select parentGeneSets)
            .Sequence()
            .Map(r => r.ToSeq().Flatten())
        from geneSets in CatletGeneCollecting.CollectGenes(catletConfig)
            .MapT(geneId => geneId.GeneIdentifier.GeneSet)
            .Map(l => l.Distinct())
        select parentGeneSets.Append(geneSets);


    public static Either<Error, CatletConfig> ResolveGenesetIdentifiers(
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedDrives in catletConfig.Drives.ToSeq()
            .Map(driveConfig => ResolveGenesetIdentifiers(driveConfig, genepoolReader))
            .Sequence()
        from resolvedFodder in catletConfig.Fodder.ToSeq()
            .Map(fodderConfig => ResolveGenesetIdentifiers(fodderConfig, genepoolReader))
            .Sequence()
        select catletConfig.CloneWith(c =>
        {
            c.Drives = resolvedDrives.ToArray();
            c.Fodder = resolvedFodder.ToArray();
        });

    private static Either<Error, FodderConfig> ResolveGenesetIdentifiers(
        FodderConfig fodderConfig,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedGeneIdentifier in ResolveGenesetIdentifiers(fodderConfig.Source, genepoolReader)
        select fodderConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Map(id => id.Value)
                .IfNoneUnsafe((string)null);
        });

    private static Either<Error, CatletDriveConfig> ResolveGenesetIdentifiers(
        CatletDriveConfig driveConfig,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedGeneIdentifier in ResolveGenesetIdentifiers(driveConfig.Source, genepoolReader)
        select driveConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Map(id => id.Value)
                .IfNoneUnsafe((string)null);
        });


    private static Either<Error, Option<GeneIdentifier>> ResolveGenesetIdentifiers(
        string geneIdentifier,
        ILocalGenepoolReader genepoolReader) =>
        from validGeneIdentifier in Optional(geneIdentifier)
            .Filter(notEmpty)
            .Map(GeneIdentifier.NewEither)
            .Sequence()
        from resolvedGeneSetIdentifier in validGeneIdentifier
            .Map(id => ResolveGeneSetIdentifier(id.GeneSet, genepoolReader))
            .Sequence()
        select from geneSetId in resolvedGeneSetIdentifier
               from geneId in validGeneIdentifier
               select new GeneIdentifier(geneSetId, geneId.GeneName);

    private static Either<Error, GeneSetIdentifier> ResolveGeneSetIdentifier(
        GeneSetIdentifier identifier,
        ILocalGenepoolReader genepoolReader,
        Seq<GeneSetIdentifier> processedReferences = default) =>
        from _ in guardnot(processedReferences.Contains(identifier),
                Error.New("Circular reference detected: "
                          + string.Join(" -> ", processedReferences.Add(identifier))))
            .ToEither()
        from __ in guardnot(processedReferences.Count > 4,
                Error.New("The reference chain is too long: "
                          + string.Join(" -> ", processedReferences.Add(identifier))))
            .ToEither()
        from optionalReference in genepoolReader.GetGenesetReference(identifier)
        from result in optionalReference.Match(
            Some: reference => ResolveGeneSetIdentifier(reference, genepoolReader,
                processedReferences.Add(identifier)),
            None: () => identifier
        )
        select result;

    private static Either<Error, CatletConfig> ReadCatletConfig(
        GeneSetIdentifier geneSetId,
        ILocalGenepoolReader genepoolReader) =>
        from json in genepoolReader.ReadGeneContent(
            GeneType.Catlet, new GeneIdentifier(geneSetId, GeneName.New("catlet")))
        from config in Try(() =>
        {
            var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(json);
            return CatletConfigDictionaryConverter.Convert(configDictionary);
        }).ToEither(ex => Error.New($"Could not deserialize catlet config {geneSetId}", Error.New(ex)))
        select config;

    public static Either<Error, CatletConfig> BreedRecursively(
        GeneSetIdentifier geneSetId,
        LocalGenepoolReader genepoolReader) =>
        from catletConfig in ReadConfig(geneSetId, genepoolReader)
        from breedConfig in BreedRecursively(catletConfig.Config, genepoolReader)
        select breedConfig;

    public static Either<Error, CatletConfig> BreedRecursively(
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        from parentConfig in ReadConfig(catletConfig.Parent, genepoolReader)
        from result in parentConfig.Match(
            Some: pCfg => from bredPCfg in BreedRecursively(pCfg.Config, genepoolReader)
                from bredCfg in Breed(bredPCfg, pCfg.Id, catletConfig, genepoolReader)
                select bredCfg,
            None: () => ResolveGenesetIdentifiers(catletConfig, genepoolReader)
        )
        select result;

    private static Either<Error, Option<(GeneSetIdentifier Id, CatletConfig Config)>> ReadConfig(
        string geneSetId,
        ILocalGenepoolReader genepoolReader) =>
        from validId in Optional(geneSetId)
            .Filter(notEmpty)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
        from result in validId.Map(id => ReadConfig(id, genepoolReader))
            .Sequence()
        select result;

    private static Either<Error, (GeneSetIdentifier Id, CatletConfig Config)> ReadConfig(
        GeneSetIdentifier geneSetId,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedId in ResolveGeneSetIdentifier(geneSetId, genepoolReader)
        from config in ReadCatletConfig(resolvedId, genepoolReader)
        select (resolvedId, config);

    public static Either<Error, CatletConfig> Breed(
        CatletConfig parentConfig,
        GeneSetIdentifier parentIdentifier,
        CatletConfig catletConfig,
        ILocalGenepoolReader genepoolReader) =>
        // TODO implement properly
        from resolvedParentConfig in ResolveGenesetIdentifiers(parentConfig, genepoolReader)
            .MapLeft(errors => Error.New("Could not resolve parent genes during breeding.", Error.Many(errors)))
        from resolvedCatletConfig in ResolveGenesetIdentifiers(catletConfig, genepoolReader)
            .MapLeft(errors => Error.New("Could not resolve child genes during breeding.", Error.Many(errors)))
        select parentConfig.Breed(catletConfig, parentIdentifier.Value);
}