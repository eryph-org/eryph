using System;
using System.Linq;
using Eryph.Core;
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
        var providerConfig = hostInfo.NetworkProviderConfiguration.NetworkProviders.FirstOrDefault(x => x.Name == providerName);
        if (providerConfig == null || providerConfig.Type == NetworkProviderType.Invalid) 
            return null;

        return providerConfig.Type == NetworkProviderType.Flat 
            ? providerConfig.SwitchName 
            : EryphConstants.OverlaySwitchName;
    }
}