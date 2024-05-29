using System;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class IpAssignment
{
    public Guid Id { get; set; }
    
    public Guid? SubnetId { get; set; }
    
    public Subnet? Subnet { get; set; }

    public string? IpAddress { get; set; }

    public Guid NetworkPortId { get; set; }
    
    public NetworkPort NetworkPort { get; set; } = null!;
}
