using System;

namespace Eryph.StateDb.Model;

public class CatletNetworkAdapter
{
    public required string Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;
    
    public required string Name { get; set; }

    public required string? SwitchName { get; set; }
    
    public string? NetworkProviderName { get; set; }

    public string? MacAddress { get; set; }
}
