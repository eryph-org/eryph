using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class CatletBreeding
{
    public static Validation<Error, CatletConfig> ResolveGenesetIdentifiers(
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

    private static Validation<Error, FodderConfig> ResolveGenesetIdentifiers(
        FodderConfig fodderConfig,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedGeneIdentifier in ResolveGenesetIdentifiers(fodderConfig.Source, genepoolReader)
        select fodderConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Value;
        });

    private static Validation<Error, CatletDriveConfig> ResolveGenesetIdentifiers(
        CatletDriveConfig driveConfig,
        ILocalGenepoolReader genepoolReader) =>
        from resolvedGeneIdentifier in ResolveGenesetIdentifiers(driveConfig.Source, genepoolReader)
        select driveConfig.CloneWith(c =>
        {
            c.Source = resolvedGeneIdentifier.Value;
        });


    private static Validation<Error, GeneIdentifier> ResolveGenesetIdentifiers(
        string geneIdentifier,
        ILocalGenepoolReader genepoolReader) =>
        from validGeneIdentifier in GeneIdentifier.NewValidation(geneIdentifier)
        from resolvedGeneSetIdentifier in ResolveGeneSetIdentifier(
            validGeneIdentifier.GeneSet, genepoolReader).ToValidation()
        select new GeneIdentifier(resolvedGeneSetIdentifier, validGeneIdentifier.GeneName);

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

    public static CatletConfig BreedAndResolve(
        CatletConfig catletConfig,
        Option<CatletConfig> parentConfig,
        ILocalGenepoolReader genepoolReader) => throw new NotImplementedException();
}