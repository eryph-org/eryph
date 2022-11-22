using System;
using Eryph.Core;

namespace Eryph.VmManagement.Data.Full;

public class HostNetworkAdapter
{
    public Guid InterfaceGuid { get; init; }

    public string Name { get; init; }
}

public class NetNat
{

    public string Name { get; init; }

    public string InternalIPInterfaceAddressPrefix { get; init; }
}