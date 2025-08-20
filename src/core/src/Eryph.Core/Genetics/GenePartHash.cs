using Eryph.ConfigModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class GenePartHash : EryphName<GenePartHash>
{
    public GenePartHash(string value) : base(value)
    {
        (Algorithm, Hash) = ValidOrThrow(
            from nonEmptyValue in Validations<GenePartHash>.ValidateNotEmpty(value)
            let parts = nonEmptyValue.Split(':')
            from _1 in guard(parts.Length == 2, Error.New("The gene part hash is invalid."))
            let algorithm = parts[0]
            let normalizedAlgorithm = algorithm.ToLowerInvariant()
            from _2 in guard(normalizedAlgorithm is "sha1", Error.New($"The algorithm '{algorithm}' is invalid."))
            let hash = parts[1].ToLowerInvariant()
            // TODO Would be better to share this code with the gene pool client
            from _3 in guard(
                hash.ToSeq().All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9')
                && hash.Length == 40,
                Error.New("The hash must be a 40 character long hexadecimal string."))
            select (normalizedAlgorithm, hash));
    }

    public string Algorithm { get; }

    public string Hash { get; }
}
