using System;
using Ardalis.Specification;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public static class ProjectSpecs
{
    public sealed class GetByName : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetByName(Guid tenantId, string name)
        {
            Query.Where(x => x.TenantId == tenantId && x.Name == name);
        }


    }

    public sealed class GetById : Specification<Project>, ISingleResultSpecification<Project>
    {
        public GetById(Guid projectId)
        {
            Query.Where(x => x.Id == projectId);
        }


    }

    public sealed class GetAll : Specification<Project>
    {



    }

}