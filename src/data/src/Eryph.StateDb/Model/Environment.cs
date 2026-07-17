using System;

namespace Eryph.StateDb.Model;

/// <summary>
/// A named environment and the <see cref="Model.Site"/> which realizes it: the realized form of the
/// operator-authored environment catalog.
/// </summary>
/// <remarks>
/// The authored catalog is YAML, distributed to the components as a configuration domain. This is
/// what it is realized into, mirroring how the network provider configuration is realized into
/// provider subnets and IP pools. Resolving the site of an environment reads this table and nothing
/// else, so it does not depend on the configuration exchange — which matters because the database is
/// seeded long before any configuration is distributed.
/// <para>
/// <see cref="Resource.Environment"/> deliberately has no foreign key to this: the resources use the
/// table-per-concrete-type strategy, which would replicate the constraint into every resource table.
/// An environment is named on a resource; it is not owned by this catalog.
/// </para>
/// </remarks>
public class Environment
{
    /// <summary>The environment name, lower-cased. This is the key: an environment is global.</summary>
    public required string Name { get; set; }

    public Guid SiteId { get; set; }

    public Site Site { get; set; } = null!;
}
