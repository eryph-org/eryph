using LanguageExt;

namespace Eryph.Core;

public interface IGenePoolPathProvider
{
    Aff<string> GetGenePoolPath();
}
