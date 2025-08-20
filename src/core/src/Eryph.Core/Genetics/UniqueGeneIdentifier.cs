using System;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

/// <summary>
/// This record uniquely identifies a gene as it also
/// specifies the gene's architecture.
/// </summary>
public class UniqueGeneIdentifier : EryphName<UniqueGeneIdentifier>
{
    public UniqueGeneIdentifier(string value) : base(Normalize(value))
    {
        (GeneType, Id, Architecture) = ValidOrThrow(
            from result in Validate(value)
            select result);
    }

    public UniqueGeneIdentifier(GeneType geneType, GeneIdentifier id, Architecture architecture)
        : this(Format(geneType, id, architecture))
    {
    }

    public GeneType GeneType { get; }

    public GeneIdentifier Id { get; }

    public Architecture Architecture { get; }

    private static string Normalize(string value) =>
        Optional(value)
            .Bind(s => Validate(s).ToOption())
            .Map(t => Format(t.GeneType, t.Id, t.Architecture))
            .IfNoneUnsafe(value);

    private static string Format(GeneType geneType, GeneIdentifier id, Architecture architecture) =>
        $"{geneType}::{id}[{architecture}]";

    private static Validation<Error, (GeneType GeneType, GeneIdentifier Id, Architecture Architecture)> Validate(
        string value) =>
        from nonEmptyValue in Validations<UniqueGeneIdentifier>.ValidateNotEmpty(value)
        let parts = nonEmptyValue.Split(["::", "[", "]"], StringSplitOptions.None)
        from _1 in guard(parts.Length == 4 && parts[3] == "",
                Error.New("The unique gene identifier is invalid."))
            .ToValidation()
        from geneType in parseEnumIgnoreCase<GeneType>(parts[0])
            .ToValidation(Error.New($"The gene type {parts[0]} is invalid."))
        from id in GeneIdentifier.NewValidation(parts[1])
        from architecture in Architecture.NewValidation(parts[2])
        select (geneType, id, architecture);
}
