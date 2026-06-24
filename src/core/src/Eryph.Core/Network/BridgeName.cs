using Eryph.ConfigModel;

namespace Eryph.Core.Network;

public class BridgeName : EryphName<BridgeName>
{
    public BridgeName(string value) : base(value)
    {
        ValidOrThrow(Validations<BridgeName>.ValidateCharacters(
                         value,
                         allowDots: false,
                         allowHyphens: true,
                         allowUnderscores: false,
                         allowSpaces: false)
                     | Validations<BridgeName>.ValidateLength(value, 3, 15));
    }
}
