using Eryph.ModuleCore.Configuration;

namespace Eryph.Runtime.Zero.Configuration;

public class ZeroNetworkConfigDefaults : INetworkConfigDefaults
{
    public bool MacAddressSpoofing => true;
}
