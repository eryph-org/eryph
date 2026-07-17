using System;

namespace Eryph.StateDb.Model;

/// <summary>
/// Implemented by resources which are realized by exactly one <see cref="Model.Site"/>.
/// </summary>
/// <remarks>
/// <see cref="SiteId"/> is resolved from <see cref="Resource.Environment"/> when the
/// resource is created and pinned there. It is then the authoritative answer to which
/// site the resource lives in and must never be re-derived: the environment to site
/// binding can be re-authored, but the location of an existing resource cannot change
/// because of it.
/// <see cref="CatletFarm"/> is the exception: a host is not placed but assigned, so
/// its site follows its registration and is reconciled on every inventory round. A
/// host's site is an input to placement; it is never the source of the site of a
/// resource found on it, which belongs to the site of its own environment.
/// <see cref="CatletSpecification"/> is deliberately not site bound at all. A
/// specification is project level and deploys into many environments, so it has no
/// single site of its own.
/// </remarks>
public interface ISiteBound
{
    Guid SiteId { get; set; }

    Site Site { get; set; }
}
