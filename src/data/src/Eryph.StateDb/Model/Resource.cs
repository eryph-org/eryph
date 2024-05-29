using System;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public abstract class Resource
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public ResourceType ResourceType { get; set; }

    public required string Name { get; set; }
}
