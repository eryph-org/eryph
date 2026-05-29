using System;
using Ardalis.Specification;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

internal static class ComponentRegistrationSpecs
{
    public sealed class GetByComponentId : Specification<ComponentRegistration>,
        ISingleResultSpecification<ComponentRegistration>
    {
        public GetByComponentId(Guid componentId)
        {
            Query.Where(x => x.ComponentId == componentId);
        }
    }
}

internal static class ConfigRecordSpecs
{
    public sealed class GetByDomain : Specification<ConfigRecord>,
        ISingleResultSpecification<ConfigRecord>
    {
        public GetByDomain(ConfigDomain domain)
        {
            Query.Where(x => x.Domain == domain);
        }
    }
}
