using System;

namespace Eryph.StateDb.Model;

/// <summary>
/// The substrate which realizes environments: hosts, storage and the network
/// stack. A site spans projects and environments; an environment is realized by
/// exactly one site.
/// </summary>
public class Site
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
}
