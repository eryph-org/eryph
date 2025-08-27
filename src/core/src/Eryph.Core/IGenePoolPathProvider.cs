using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

public interface IGenePoolPathProvider
{
    Aff<string> GetGenePoolPath();
}
