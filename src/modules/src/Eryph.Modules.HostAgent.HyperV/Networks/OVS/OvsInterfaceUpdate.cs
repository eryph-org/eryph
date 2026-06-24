using System;

namespace Eryph.Modules.HostAgent.Networks.OVS;

public record OvsInterfaceUpdate(
    string Name,
    Guid InterfaceId,
    string ConfiguredName);
