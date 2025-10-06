using System;
using System.Net;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

/// <summary>
/// Represents a route table entry on the host.
/// </summary>
/// <param name="InterfaceId">
/// The ID of the network adapter to which the route table entry belongs.
/// Can be <see cref="OptionNone"/> for routes which do not belong to
/// an adapter (e.g. loopback routes) or when the adapter cannot be determined.
/// </param>
/// <param name="Destination">
/// The destination network for the route.
/// </param>
/// <param name="NextHop">
/// The IP address of the next hop for the route.
/// </param>
public record HostRouteInfo(
    Option<Guid> InterfaceId,
    IPNetwork2 Destination,
    IPAddress NextHop);
