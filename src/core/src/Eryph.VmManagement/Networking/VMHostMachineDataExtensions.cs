using System;
using System.Linq;
using Eryph.Core.Network;
using Eryph.Resources.Machines;
using JetBrains.Annotations;

namespace Eryph.VmManagement.Networking;

public static class VMHostMachineDataExtensions
{
    [CanBeNull]
    public static NetworkProvider FindNetworkProvider(this VMHostMachineData hostInfo, Guid vmSwitchId,
        [CanBeNull] string portName)
    {

        return null;
    }

    [CanBeNull]
    public static string FindSwitchName(this VMHostMachineData hostInfo, string providerName)
    {

        return "eryph_overlay";
    }
}