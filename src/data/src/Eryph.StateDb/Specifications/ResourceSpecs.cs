using System;
using System.Collections.Generic;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.StateDb.Model;
using JetBrains.Annotations;

namespace Eryph.StateDb.Specifications;

public static class ResourceSpecs<T> where T: Resource
{
    public sealed class GetAll : Specification<T>
    {
        public GetAll(
            AuthContext authContext,
            IEnumerable<Guid> sufficientRoles,
            Guid? filteredProject,
            Action<ISpecificationBuilder<T>>? customizeAction = null)
        {
            authContext.QueryResourceAccess(Query, sufficientRoles);

            if (filteredProject!= null) 
                Query.Where(x => x.ProjectId == filteredProject.GetValueOrDefault());

            Query.OrderBy(x => x.Id);
            customizeAction?.Invoke(Query);
        }
    }

    public sealed class GetByName : Specification<T>
    {
        public GetByName(string name, Action<ISpecificationBuilder<T>>? customizeAction = null)
        {
            Query.Where(x => x.Name == name);
            customizeAction?.Invoke(Query);
        }
    }

    public sealed class GetById : Specification<T>, ISingleResultSpecification<T>
    {
        public GetById(Guid id, Action<ISpecificationBuilder<T>>? customizeAction = null)
        {
            Query.Where(x => x.Id == id).Include(x => x.Project);
            customizeAction?.Invoke(Query);

        }

        public GetById(Guid id, AuthContext authContext, IEnumerable<Guid> sufficientRoles,
            Action<ISpecificationBuilder<T>>? customizeAction = null)
        {
            Query.Where(x => x.Id == id);
            authContext.QueryResourceAccess(Query, sufficientRoles);
                
            customizeAction?.Invoke(Query);

        }
    }
}
