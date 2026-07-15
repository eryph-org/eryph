using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class SiteSpecs
{
    public sealed class GetByName : Specification<Site>, ISingleResultSpecification
    {
        public GetByName(string name)
        {
            Query.Where(x => x.Name == name.ToLowerInvariant());
        }
    }
}
