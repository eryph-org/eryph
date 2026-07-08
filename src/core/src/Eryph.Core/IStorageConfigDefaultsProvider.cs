using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

/// <summary>
/// Supplies the default-scope <see cref="StorageConfig"/> the controller distributes when nothing is
/// operator-authored for a scope. The implementation is host-wired, not selected by a flag: the split
/// runtime reads the central controller settings, while eryph-zero reads the local
/// <c>agentsettings.yml</c> (which stays the authoritative storage config there, shared by the agent
/// and the gene pool).
/// </summary>
public interface IStorageConfigDefaultsProvider
{
    EitherAsync<Error, StorageConfig> GetDefaultStorageConfig();
}
