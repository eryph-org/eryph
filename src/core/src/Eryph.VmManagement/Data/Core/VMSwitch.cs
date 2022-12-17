using System;
using Eryph.ConfigModel;
using Eryph.Core;

namespace Eryph.VmManagement.Data.Core;

public class VMSwitch
{
    [PrivateIdentifier]

    public Guid Id { get; init; }

    [PrivateIdentifier]
    public string Name { get; init; }

    public Guid[] NetAdapterInterfaceGuid { get; init; }

}

public class VMSwitchExtension
{

    public string Id { get; init; }

    public bool Enabled { get; init; }


    [PrivateIdentifier]
    public string SwitchName { get; init; }

    public Guid SwitchId { get; init; }


}

