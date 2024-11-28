using System;

namespace Eryph.StateDb.Model;

public class CatletNetworkAdapter
{
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

    public required string MacAddress { get; set; }
}
