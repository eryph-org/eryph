using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool.Genetics;

public class GeneHash : EryphName<GeneHash>
{
    public GeneHash(string value) : base(value)
    {
        (Algorithm, Hash) = ValidOrThrow(
            from nonEmptyValue in Validations<GenePartHash>.ValidateNotEmpty(value)
            let parts = nonEmptyValue.Split(':')
            from _1 in guard(parts.Length == 2, Error.New("The gene hash is invalid."))
                .ToValidation()
            let algorithm = parts[0]
            let normalizedAlgorithm = algorithm.ToLowerInvariant()
            from _2 in guard(normalizedAlgorithm is "sha256", Error.New($"The algorithm '{algorithm}' is invalid."))
            from gene in Gene.NewValidation(parts[1])
            select (normalizedAlgorithm, gene));
    }

    public string Algorithm { get; }

    public Gene Hash { get; }
}
