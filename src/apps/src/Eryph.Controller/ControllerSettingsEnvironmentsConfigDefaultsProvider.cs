using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Controller;

/// <summary>
/// Split-runtime default environment catalog. Environments are defined centrally here — authored via
/// the management API and distributed — so there is no settings file to derive them from and the
/// catalog starts out holding only the reserved default environment.
/// </summary>
internal sealed class ControllerSettingsEnvironmentsConfigDefaultsProvider
    : IEnvironmentsConfigDefaultsProvider
{
    public EitherAsync<Error, EnvironmentsConfig> GetDefaultEnvironmentsConfig() =>
        RightAsync<Error, EnvironmentsConfig>(new EnvironmentsConfig());
}
