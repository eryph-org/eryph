using System;

namespace Eryph.VmManagement.Data.Full;

public class NetRoute
{
    public string DestinationPrefix { get; init; }
    
    public string NextHop { get; init; }

    public string InterfaceAlias { get; init; }

    public int InterfaceIndex { get; set; }
}
