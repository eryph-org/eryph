using System;

// The change tracking in the controller module must be updated when modifying this entity.
namespace Eryph.StateDb.Model;

public class CatletMetadata
{
    public Guid Id { get; set; }

    public string Metadata { get; set; } = "";
}
