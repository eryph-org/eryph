using System;
using System.Linq;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Resources.Machines;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.VmManagement.Networking;

public static class VMHostMachineDataExtensions
{
    public static Option<string> FindSwitchName(
        this VMHostMachineData hostInfo,
        string providerName) =>
        hostInfo.NetworkProviderConfiguration.NetworkProviders.ToSeq()
            .Find(p => p.Name == providerName)
            .Filter(p => p.Type != NetworkProviderType.Invalid)
            .Map(p => p.Type == NetworkProviderType.Flat
                ? p.SwitchName
                : EryphConstants.OverlaySwitchName);
}
