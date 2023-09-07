using System;
using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core;

public class VMSwitch
{
    [PrivateIdentifier]

    public Guid Id { get; init; }

    [PrivateIdentifier]
    public string Name { get; init; }

    public string NetAdapterInterfaceDescription { get; init; }
    public Guid[] NetAdapterInterfaceGuid { get; set; }

}