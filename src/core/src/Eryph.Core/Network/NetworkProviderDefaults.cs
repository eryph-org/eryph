using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core.Network;

public class NetworkProviderDefaults
{
    public bool DisableDhcpGuard { get; set; }

    public bool DisableRouterGuard { get; set; }

    public bool MacAddressSpoofing { get; set; }
}
