using System;

namespace Eryph.StateDb.Model;

/// <summary>
/// Implemented by resources which are realized by exactly one <see cref="Model.Site"/>.
/// </summary>
/// <remarks>
/// <see cref="SiteId"/> is pinned when the resource is created and is the
/// authoritative answer to which site the resource lives in. It must never be
/// re-derived from <see cref="Resource.Environment"/>: the environment to site
/// binding can be re-authored, but the location of an existing resource cannot
/// change because of it.
/// <see cref="CatletSpecification"/> is deliberately not site bound. A
/// specification is project level and deploys into many environments, hence into
/// many sites.
/// </remarks>
public interface ISiteBound
{
    Guid SiteId { get; set; }

    Site Site { get; set; }
}
