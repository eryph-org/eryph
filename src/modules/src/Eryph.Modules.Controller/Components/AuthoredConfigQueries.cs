using Ardalis.Specification;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

internal static class AuthoredConfigSpecs
{
    /// <summary>The current (highest-version) authored value for a domain/scope, or none.</summary>
    public sealed class GetCurrent : Specification<AuthoredConfig>,
        ISingleResultSpecification<AuthoredConfig>
    {
        public GetCurrent(ConfigDomain domain, string scope)
        {
            Query.Where(x => x.Domain == domain && x.Scope == scope)
                .OrderByDescending(x => x.Version)
                .Take(1);
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
