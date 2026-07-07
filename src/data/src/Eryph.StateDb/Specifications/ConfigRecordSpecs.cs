using Ardalis.Specification;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ConfigRecordSpecs
{
    /// <summary>The materialized (effective) record for a domain, or none.</summary>
    public sealed class GetByDomain : Specification<ConfigRecord>,
        ISingleResultSpecification<ConfigRecord>
    {
        public GetByDomain(ConfigDomain domain)
        {
            Domain = domain;
            Query.Where(x => x.Domain == domain);
        }

        public ConfigDomain Domain { get; }
    }

    /// <summary>All materialized records (one per domain).</summary>
    public sealed class GetAll : Specification<ConfigRecord>
    {
        public GetAll()
        {
            Query.OrderBy(x => x.Domain);
        }
    }
}
