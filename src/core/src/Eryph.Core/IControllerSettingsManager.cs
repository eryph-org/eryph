using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

/// <summary>
/// Reads and writes the controller-owned settings file (the controller-scoped
/// counterpart to <see cref="IVmHostAgentConfigurationManager"/>).
/// </summary>
public interface IControllerSettingsManager
{
    EitherAsync<Error, string> GetCurrentConfigurationYaml();

    EitherAsync<Error, ControllerSettings> GetCurrentConfiguration();

    EitherAsync<Error, Unit> SaveConfigurationYaml(string config);

    EitherAsync<Error, Unit> SaveConfiguration(ControllerSettings config);
}
