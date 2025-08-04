using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface IGenePoolPathProvider
{
    EitherAsync<Error, string> GetGenePoolPath();
}
