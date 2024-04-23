using System;
using System.Collections.Generic;
using System.Net;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;

public interface IHostNetworkCommands<RT> where RT : struct, HasCancel<RT>
{
    Aff<RT, Seq<VMSwitch>> GetSwitches();
    Aff<RT, Option<VMSystemSwitchExtension>> GetInstalledSwitchExtension();
    Aff<RT, Seq<VMSwitchExtension>> GetSwitchExtensions();
    Aff<RT, Unit> DisableSwitchExtension(Guid switchId);
    Aff<RT, Unit> EnableSwitchExtension(Guid switchId);

    Aff<RT, Seq<HostNetworkAdapter>> GetPhysicalAdapters();
    Aff<RT, Seq<string>> GetAdapterNames();

    Aff<RT, Seq<NetNat>> GetNetNat();
    Aff<RT, Unit> EnableBridgeAdapter(string adapterName);
    Aff<RT, Unit> ConfigureAdapterIp(string adapterName, IPAddress ipAddress, IPNetwork2 network);
    Aff<RT, Seq<TypedPsObject<VMNetworkAdapter>>> GetNetAdaptersBySwitch(Guid switchId);
    Aff<RT, Unit> DisconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters);
    Aff<RT, Unit> ReconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters, string switchName);
    Aff<RT, Unit> CreateOverlaySwitch(IEnumerable<string> adapters);

    Aff<RT, Option<OverlaySwitchInfo>> FindOverlaySwitch(
        Seq<VMSwitch> vmSwitches,
        Seq<HostNetworkAdapter> adapters);

    Aff<RT, Unit> RemoveOverlaySwitch();
    Aff<RT, Unit> RemoveNetNat(string natName);
    Aff<RT, Unit> WaitForBridgeAdapter(string bridgeName);
    Aff<RT,Unit> AddNetNat(string natName, IPNetwork2 network);

    Aff<RT, Seq<NetIpAddress>> GetAdapterIpV4Address(string adapterName);

}