using System;
using JetBrains.Annotations;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class CatletNetworkPort : VirtualNetworkPort
{
    // We reference the catlet metadata instead of the catlet itself
    // as the network port configuration can be reseeded during startup.
    // At this point, the catlet data is not yet available as we only
    // persist the metadata to config files.
    // The foreign key exists to ensure consistent data but the network
    // ports are only very rarely accessed from the catlet data or metadata.
    // In this case, the network ports can be loaded manually.
    public Guid CatletMetadataId { get; set; }
}