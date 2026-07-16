using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core;

/// <summary>
/// Supplies the environment catalog the controller distributes until the domain is operator-authored.
/// The implementation is host-wired, not selected by a flag: eryph-zero derives it from the local
/// <c>agentsettings.yml</c>, which is where its environments have always been declared, while the
/// split runtime authors them centrally and therefore starts from the reserved default alone.
/// </summary>
public interface IEnvironmentsConfigDefaultsProvider
{
    EitherAsync<Error, EnvironmentsConfig> GetDefaultEnvironmentsConfig();
}
