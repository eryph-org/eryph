using Ardalis.Specification;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class AuthoredConfigSpecs
{
    /// <summary>The current (highest-version) authored value for a domain/scope, or none.</summary>
    public sealed class GetCurrent : Specification<AuthoredConfig>,
        ISingleResultSpecification<AuthoredConfig>
    {
        public GetCurrent(ConfigDomain domain, string scope)
        {
            Domain = domain;
            Scope = scope;
            Query.Where(x => x.Domain == domain && x.Scope == scope)
                .OrderByDescending(x => x.Version)
                .Take(1);
        }

        public ConfigDomain Domain { get; }

        public string Scope { get; }
    }

    /// <summary>A specific authored version for a domain/scope, or none.</summary>
    public sealed class GetByVersion : Specification<AuthoredConfig>,
        ISingleResultSpecification<AuthoredConfig>
    {
        public GetByVersion(ConfigDomain domain, string scope, long version)
        {
            Query.Where(x => x.Domain == domain && x.Scope == scope && x.Version == version);
        }
    }

    /// <summary>The full version history for a domain/scope, newest first.</summary>
    public sealed class GetHistory : Specification<AuthoredConfig>
    {
        public GetHistory(ConfigDomain domain, string scope)
        {
            Query.Where(x => x.Domain == domain && x.Scope == scope)
                .OrderByDescending(x => x.Version);
        }
    }
}
