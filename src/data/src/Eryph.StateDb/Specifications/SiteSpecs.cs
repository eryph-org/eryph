using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class SiteSpecs
{
    public sealed class GetByName : Specification<Site>, ISingleResultSpecification
    {
        public GetByName(string? name)
        {
            // A site is always named, so no name matches nothing rather than throwing on a caller
            // which did not check first.
            Query.Where(x => name != null && x.Name == name.ToLowerInvariant());
        }
    }

    public sealed class GetByIds : Specification<Site>
    {
        public GetByIds(IReadOnlyList<Guid> ids)
        {
            Query.Where(x => ids.Contains(x.Id));
        }
    }
}
