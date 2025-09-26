using System;
using System.Net;
using LanguageExt;

namespace Eryph.Modules.HostAgent.Networks;

public record HostRouteInfo(
    Option<Guid> InterfaceId,
    IPNetwork2 Destination,
    IPAddress NextHop);
