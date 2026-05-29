using Eryph.Core;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal sealed class SingleHostClusterTopologyProvider : IClusterTopologyProvider
{
    public string ChassisGroupName => EryphConstants.Networking.LocalChassisGroupName;

    public Seq<(string ChassisName, short Priority)> GetChassis() =>
        Seq1((EryphConstants.Networking.LocalChassisName, (short)1));
}
