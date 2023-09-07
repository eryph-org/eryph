using System;
using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core;

public class VMSwitchExtension
{

    public string Id { get; init; }

    public bool Enabled { get; init; }


    [PrivateIdentifier]
    public string SwitchName { get; init; }

    public Guid SwitchId { get; init; }


}