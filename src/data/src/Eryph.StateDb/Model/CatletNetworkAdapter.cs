using System;

namespace Eryph.StateDb.Model;

public class CatletNetworkAdapter
{
    /// <summary>
    /// The Hyper-V ID of the network adapter.
    /// </summary>
    public required string Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;
    
    public required string Name { get; set; }

    /// <summary>
    /// The name of the Hyper-V switch to which the network
    /// adapter is connected. Can be <see langword="null"/>
    /// when the adapter is not connected to a switch.
    /// </summary>
    public required string? SwitchName { get; set; }

    /// <summary>
    /// The MAC address of the Hyper-V network adapter.
    /// </summary>
    /// <remarks>
    /// The MAC address can be <see langword="null"/>. This happens
    /// e.g. when the adapter uses a dynamic MAC address and the
    /// VM has not been started yet. Eryph always assigns MAC
    /// addresses statically. Hence, <see langword="null"/> should
    /// only occur for adapters which have been added or modified
    /// outside eryph.
    /// </remarks>
    public required string? MacAddress { get; set; }
}
