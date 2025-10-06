using System;

namespace Eryph.VmManagement.Data.Full;

public class HostNetworkAdapter
{
    public Guid InterfaceGuid { get; init; }

    public int InterfaceIndex { get; init; }

    public string Name { get; init; }

    public bool Virtual { get; init; }
}
