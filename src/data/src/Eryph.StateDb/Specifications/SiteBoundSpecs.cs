using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

/// <summary>
/// Specifications over the resources which are pinned to a site. The constraint is what makes the
/// site accessible to the query: a cast to <see cref="ISiteBound"/> inside the expression could not
/// be translated to SQL.
/// </summary>
public static class SiteBoundSpecs<T> where T : Resource, ISiteBound
{
    /// <summary>
    /// The resources of this type pinned to a site, across all tenants and projects. Used by the
    /// controller to refuse removing a site which is still in use — not for reads on behalf of a
    /// user, which must go through the access-scoped specifications in <see cref="ResourceSpecs{T}"/>.
    /// </summary>
    public sealed class GetBySiteUnscoped : Specification<T>
    {
        public GetBySiteUnscoped(Guid siteId)
        {
            Query.Where(x => x.SiteId == siteId);
        }
    }
}
