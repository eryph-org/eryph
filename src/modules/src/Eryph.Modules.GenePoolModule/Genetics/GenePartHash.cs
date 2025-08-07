using Eryph.ConfigModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

public class GenePartHash : EryphName<GenePartHash>
{
    public GenePartHash(string value) : base(Normalize(value))
    {
        (Algorithm, Hash) = ValidOrThrow(
            from nonEmptyValue in Validations<GenePartHash>.ValidateNotEmpty(value)
            let parts = nonEmptyValue.Split(':')
            from _1 in guard(parts.Length == 2, Error.New("The gene part hash is invalid."))
                .ToValidation()
            let algorithm = parts[0].ToLowerInvariant()
            from _2 in guard(algorithm is "sha1", Error.New($"The algorithm '{parts[0]}' is invalid."))
            from gene in GenePart.NewValidation(parts[1])
            select (algorithm, gene));
    }

    public string Algorithm { get; }

    public GenePart Hash { get; }

    private static string Normalize(string value) => value?.ToLowerInvariant();
}
