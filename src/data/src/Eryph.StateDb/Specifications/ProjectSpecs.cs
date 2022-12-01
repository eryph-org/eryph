using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProjectSpecs
{
    public sealed class GetByName : Specification<Project>, ISingleResultSpecification
    {
        public GetByName(Guid tenantId, string name)
        {
            Query.Where(x => x.TenantId == tenantId && x.Name == name);
        }
    }

}