using Ardalis.Specification;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ConfigRecordSpecs
{
    /// <summary>The materialized (effective) record for a domain at a scope, or none.</summary>
    public sealed class GetByDomainAndScope : Specification<ConfigRecord>,
        ISingleResultSpecification<ConfigRecord>
    {
        public GetByDomainAndScope(ConfigDomain domain, string scope)
        {
            Domain = domain;
            Scope = scope;
            Query.Where(x => x.Domain == domain && x.Scope == scope);
        }

        public ConfigDomain Domain { get; }

        public string Scope { get; }
    }

    /// <summary>All materialized records for a domain (one per scope).</summary>
    public sealed class GetByDomain : Specification<ConfigRecord>
    {
        public GetByDomain(ConfigDomain domain)
        {
            Domain = domain;
            Query.Where(x => x.Domain == domain);
        }

        public ConfigDomain Domain { get; }
    }

    /// <summary>All materialized records (one per domain/scope).</summary>
    public sealed class GetAll : Specification<ConfigRecord>
    {
        public GetAll()
        {
            Query.OrderBy(x => x.Domain).ThenBy(x => x.Scope);
        }
    }
}
