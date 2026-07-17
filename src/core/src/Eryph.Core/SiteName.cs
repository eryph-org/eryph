using Eryph.ConfigModel;

namespace Eryph.Core;

/// <summary>
/// The name of a site. Uses the same grammar as the other eryph resource names
/// (e.g. <see cref="EnvironmentName"/>), as operators author it the same way.
/// </summary>
public class SiteName : EryphName<SiteName>
{
    public SiteName(string value) : base(value)
    {
        ValidOrThrow(Validations<SiteName>.ValidateCharacters(
                         value,
                         allowDots: false,
                         allowHyphens: true,
                         allowUnderscores: true,
                         allowSpaces: false)
                     | Validations<SiteName>.ValidateLength(value, 1, 50));
    }
}
