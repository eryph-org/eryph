using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class GeneHash : EryphName<GeneHash>
{
    public GeneHash(string value) : base(value)
    {
        (Algorithm, Hash) = ValidOrThrow(
            from nonEmptyValue in Validations<GeneHash>.ValidateNotEmpty(value)
            let parts = nonEmptyValue.Split(':')
            from _1 in guard(parts.Length == 2, Error.New("The gene hash is invalid."))
            let algorithm = parts[0]
            let normalizedAlgorithm = algorithm.ToLowerInvariant()
            from _2 in guard(normalizedAlgorithm is "sha256", Error.New($"The algorithm '{algorithm}' is invalid."))
            let hash = parts[1].ToLowerInvariant()
            from _3 in guard(
                    hash.ToSeq().All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9')
                    && hash.Length == 64,
                Error.New("The hash must be a 64 character long hexadecimal string."))
            select (normalizedAlgorithm, hash));
    }

    public string Algorithm { get; }

    public string Hash { get; }
}
