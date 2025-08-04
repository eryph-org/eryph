using System;
using System.Collections.Generic;
using System.Net;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.HostAgent.Networks;

public interface IHostNetworkCommands<RT> where RT : struct, HasCancel<RT>
{
    Aff<RT, Seq<VMSwitch>> GetSwitches();
    Aff<RT, Option<VMSystemSwitchExtension>> GetInstalledSwitchExtension();
    Aff<RT, Seq<VMSwitchExtension>> GetSwitchExtensions();
    Aff<RT, Unit> DisableSwitchExtension(Guid switchId);
    Aff<RT, Unit> EnableSwitchExtension(Guid switchId);

    Aff<RT, Seq<HostNetworkAdapter>> GetHostAdapters();

    Aff<RT, Seq<NetNat>> GetNetNat();
    Aff<RT, Unit> EnableBridgeAdapter(string adapterName);
    Aff<RT, Unit> ConfigureAdapterIp(string adapterName, IPAddress ipAddress, IPNetwork2 network);
    Aff<RT, Seq<TypedPsObject<VMNetworkAdapter>>> GetNetAdaptersBySwitch(Guid switchId);
    Aff<RT, Unit> ConnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters, string switchName);
    Aff<RT, Unit> DisconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters);
    Aff<RT, Unit> ReconnectNetworkAdapters(Seq<TypedPsObject<VMNetworkAdapter>> adapters);
    Aff<RT, Unit> CreateOverlaySwitch(Seq<string> adapters);

    Aff<RT, Unit> RemoveOverlaySwitch();
    Aff<RT, Unit> RemoveNetNat(string natName);
    Aff<RT, Unit> WaitForBridgeAdapter(string bridgeName);
    Aff<RT,Unit> AddNetNat(string natName, IPNetwork2 network);

    Aff<RT, Seq<NetIpAddress>> GetAdapterIpV4Address(string adapterName);

}