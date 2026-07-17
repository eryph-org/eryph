using Ardalis.Specification;

namespace Eryph.StateDb.Specifications;

public static class EnvironmentSpecs
{
    public sealed class GetByName : Specification<Model.Environment>, ISingleResultSpecification
    {
        public GetByName(string? name)
        {
            // An environment is always named, so no name matches nothing rather than throwing on a
            // caller which did not check first.
            Query.Where(x => name != null && x.Name == name.ToLowerInvariant());
        }
    }

    public sealed class GetBySite : Specification<Model.Environment>
    {
        public GetBySite(System.Guid siteId)
        {
            Query.Where(x => x.SiteId == siteId);
        }
    }
}
