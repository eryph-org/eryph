using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Controller;

/// <summary>
/// Split-runtime default storage config source: the central controller settings file
/// (<c>controllersettings.yml</c>), authored centrally.
/// </summary>
internal sealed class ControllerSettingsStorageConfigDefaultsProvider(
    IControllerSettingsManager settingsManager)
    : IStorageConfigDefaultsProvider
{
    public EitherAsync<Error, StorageConfig> GetDefaultStorageConfig() =>
        settingsManager.GetCurrentConfiguration().Map(settings => settings.Storage);
}
