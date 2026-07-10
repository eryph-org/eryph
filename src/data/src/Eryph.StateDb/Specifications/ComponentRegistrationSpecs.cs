using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ComponentRegistrationSpecs
{
    public sealed class GetByComponentId : Specification<ComponentRegistration>,
        ISingleResultSpecification<ComponentRegistration>
    {
        public GetByComponentId(Guid componentId)
        {
            Query.Where(x => x.ComponentId == componentId);
        }
    }

    public sealed class GetActive : Specification<ComponentRegistration>
    {
        public GetActive()
        {
            Query.Where(x => x.Status == ComponentRegistrationStatus.Active);
        }
    }

    /// <summary>All registered components, ordered by type then machine name.</summary>
    public sealed class GetAll : Specification<ComponentRegistration>
    {
        public GetAll()
        {
            Query.OrderBy(x => x.ComponentType)
                .ThenBy(x => x.MachineName);
        }
    }
}
