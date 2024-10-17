using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class GeneIdentifierExtended : EryphName<GeneIdentifierExtended>
{
    public GeneIdentifierExtended(string value) : base(Normalize(value))
    {
        (Identifier, Architecture) = ValidOrThrow(
            from nonEmptyValue in Validations<GeneIdentifierExtended>.ValidateNotEmpty(value)
            let parts = nonEmptyValue.Split('@')
            from _ in guard(parts.Length == 2, Error.New("The extended gene identifier is invalid"))
                .ToValidation()
            from id in GeneIdentifier.NewValidation(parts[0])
            from architecture in GeneArchitecture.NewValidation(parts[1])
            select (id, architecture));
    }

    public GeneIdentifierExtended(GeneIdentifier geneId, GeneArchitecture architecture)
        : this($"{geneId}@{architecture}")
    {

    }

    public GeneIdentifier Identifier { get; }

    public GeneArchitecture Architecture { get; }

    private static string Normalize(string value) =>
        !string.IsNullOrEmpty(value) && value.EndsWith("@any/any", StringComparison.OrdinalIgnoreCase)
            ? value[..^8] + "@any"
            : value;
}
