using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

/// <summary>
/// Reads and writes the node-local gene-pool storage settings (<c>genepoolsettings.yml</c>). The file
/// is the gene pool's cache of the controller-distributed storage config: the storage-config realizer
/// writes the resolved gene-pool root into it, and <c>GenePoolPathProvider</c> reads it — mirroring how
/// the agent caches its config in <c>agentsettings.yml</c>. Host-supplied (eryph-zero borrows the agent
/// settings in-process, so it has nothing separate to persist).
/// </summary>
public interface IGenePoolStorageSettingsManager
{
    EitherAsync<Error, GenePoolStoreSettings> GetCurrentSettings();

    EitherAsync<Error, Unit> SaveSettings(GenePoolStoreSettings settings);
}
