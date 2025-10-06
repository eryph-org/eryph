using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public record HostAdaptersInfo(
    HashMap<string, HostAdapterInfo> Adapters);

/// <summary>
/// Represent a network adapter on the host.
/// </summary>
/// <param name="Name">
/// The name of the network adapter
/// </param>
/// <param name="InterfaceId">
/// The ID of the network adapter
/// </param>
/// <param name="ConfiguredName">
/// The name of the network adapter at the time when it was configured
/// with Open vSwitch. This property is used to handle renamed adapters
/// gracefully.
/// </param>
/// <param name="IsPhysical">
/// Indicates whether the adapter is a physical adapter.
/// </param>
/// <param name="SwitchId">
/// The ID of the Hyper-V switch to which the adapter is connected.
/// Will be <see cref="OptionNone"/> when the adapter is not attached
/// to a Hyper-V switch.
/// </param>
public record HostAdapterInfo(
    string Name,
    Guid InterfaceId,
    Option<string> ConfiguredName,
    bool IsPhysical,
    Option<Guid> SwitchId);
