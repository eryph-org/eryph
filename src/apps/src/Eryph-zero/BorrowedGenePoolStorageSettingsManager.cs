using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

/// <summary>
/// eryph-zero has no separate gene-pool storage settings file: the in-process gene pool derives its
/// root from the agent's <c>agentsettings.yml</c> directly (<c>HyperVGenePoolPathProvider</c>), which
/// is the shared authoritative storage config there. So the storage-config realizer has nothing to
/// persist — reads return empty and writes are accepted no-ops.
/// </summary>
internal sealed class BorrowedGenePoolStorageSettingsManager : IGenePoolStorageSettingsManager
{
    public EitherAsync<Error, GenePoolStoreSettings> GetCurrentSettings() =>
        RightAsync<Error, GenePoolStoreSettings>(new GenePoolStoreSettings());

    public EitherAsync<Error, Unit> SaveSettings(GenePoolStoreSettings settings) =>
        RightAsync<Error, Unit>(unit);
}
